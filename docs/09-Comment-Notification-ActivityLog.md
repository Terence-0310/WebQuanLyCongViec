# Chức năng 9 — Bình luận (Comment), Thông báo (Notification) & Nhật ký (Activity Log)

## 1. Mục tiêu

Ba tính năng "phụ trợ" giúp phối hợp công việc theo nhóm:

| Tính năng | Mô tả |
|-----------|-------|
| **Comment** | Bình luận trao đổi ngay trong từng task. |
| **Notification** | Thông báo khi được giao task; có badge số chưa đọc; đẩy **realtime**. |
| **Activity Log** | Nhật ký ghi lại thao tác (tạo/sửa task, đổi trạng thái, bình luận, xếp lịch…). |

Kèm theo: **SignalR** đẩy thông báo và đồng bộ dữ liệu theo thời gian thực.

---

## 2. Khái niệm cần nắm để giải thích

### 2.1. SignalR (Realtime) là gì?

**SignalR** là thư viện của ASP.NET Core dùng **WebSocket** để server **chủ động đẩy**
dữ liệu xuống trình duyệt mà không cần client hỏi liên tục. Trong dự án:

| Sự kiện | Ý nghĩa |
|---------|---------|
| `"notify"` | Thông báo cá nhân (gửi đúng người qua `Clients.User`). |
| `"dataChanged"` | Dữ liệu task vừa đổi → các màn đang mở tự đồng bộ. |

### 2.2. Notification vs Activity Log khác nhau thế nào?

| | Notification | Activity Log |
|--|--------------|--------------|
| Dành cho ai | **Người nhận** (1 user cụ thể) | **Quản lý/theo dõi** (xem lại lịch sử) |
| Mục đích | "Bạn có việc mới" | "Ai đã làm gì, lúc nào" |
| Có đánh dấu đã đọc? | Có (`IsRead`) | Không |

### 2.3. Activity Log thiết kế tổng quát

Một bảng `ActivityLog` ghi mọi loại hành động nhờ cấu trúc tổng quát:

```
UserId | Action | EntityType | EntityId | Description | CreatedAt
  5    |"Created"| "Task"     |   12     | 'Tạo task "X"' | ...
```

`EntityType` + `EntityId` cho phép truy ngược "log nào thuộc task/project nào".

---

## 3. Các file liên quan

| File | Vai trò |
|------|---------|
| [Services/TaskService.cs](../Services/TaskService.cs) | `AddCommentAsync` + gọi notification/log/realtime. |
| [Services/NotificationService.cs](../Services/NotificationService.cs) | Tạo/đọc thông báo + đẩy realtime. |
| [Services/ActivityLogService.cs](../Services/ActivityLogService.cs) | Ghi & truy vấn nhật ký. |
| [Hubs/RealtimeHub.cs](../Hubs/RealtimeHub.cs) | Hub SignalR (đẩy `notify` / `dataChanged`). |
| [Controllers/NotificationsController.cs](../Controllers/NotificationsController.cs) | Trang thông báo + đánh dấu đã đọc. |
| [ViewComponents/NotificationBadgeViewComponent.cs](../ViewComponents/NotificationBadgeViewComponent.cs) | Badge số chưa đọc trên thanh trên. |
| [Models/TaskComment.cs](../Models/TaskComment.cs) / [Notification.cs](../Models/Notification.cs) / [ActivityLog.cs](../Models/ActivityLog.cs) | Các entity. |

---

## 4. Giải thích code chính

### 4.1. Thêm bình luận — `AddCommentAsync`

```csharp
var task = await Accessible(userId, seeAll).FirstOrDefaultAsync(t => t.Id == taskId); // kiểm tra quyền
if (task is null) return false;

_db.TaskComments.Add(new TaskComment
{
    TaskItemId = taskId, UserId = userId, Content = content.Trim()
});
await _db.SaveChangesAsync();

await _activity.LogAsync(userId, "Commented", "Task", taskId, $"Bình luận trong task \"{task.Title}\""); // ghi log
```

> Bình luận chỉ thêm được khi task **truy cập được** (đi qua `Accessible`) — an toàn quyền.

### 4.2. Tạo thông báo + đẩy realtime — `NotificationService.CreateAsync`

```csharp
_db.Notifications.Add(new Notification { UserId = userId, Message = message, TaskItemId = taskItemId });
await _db.SaveChangesAsync();

// Đẩy realtime tới ĐÚNG người nhận: cập nhật badge + hiện toast ngay
var unread = await GetUnreadCountAsync(userId);
await _rt.Clients.User(userId.ToString()).SendAsync("notify", new { message, unread });
```

`Clients.User(userId)` gửi đúng tới user đó (dù họ mở nhiều tab/thiết bị).

### 4.3. Đếm chưa đọc & đánh dấu đã đọc

```csharp
public Task<int> GetUnreadCountAsync(int userId) =>
    _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

public async Task MarkAllAsReadAsync(int userId) =>
    await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
        .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true)); // cập nhật hàng loạt, 1 câu SQL
```

> `ExecuteUpdateAsync` cập nhật thẳng dưới DB (1 câu UPDATE), không cần load entity lên — nhanh.

### 4.4. Ghi nhật ký tổng quát — `ActivityLogService.LogAsync`

```csharp
public async Task LogAsync(int userId, string action, string entityType, int entityId, string description)
{
    _db.ActivityLogs.Add(new ActivityLog
    {
        UserId = userId, Action = action,
        EntityType = entityType, EntityId = entityId, Description = description
    });
    await _db.SaveChangesAsync();
}
```

Truy vấn log theo nhiều đối tượng (cho trang chi tiết) — gom nhóm theo loại để tránh OR phức tạp:

```csharp
var byType = entities.GroupBy(e => e.Type)
    .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
foreach (var (type, ids) in byType)
{
    var logs = await _db.ActivityLogs.Include(a => a.User)
        .Where(a => a.EntityType == type && ids.Contains(a.EntityId)).ToListAsync();
    result.AddRange(logs);
}
return result.OrderByDescending(a => a.CreatedAt).Take(count).ToList();
```

### 4.5. Đồng bộ realtime khi task đổi — `BroadcastChangedAsync`

Trong `TaskService`, mọi thao tác thay đổi task đều gọi:

```csharp
private Task BroadcastChangedAsync() =>
    _rt.Clients.All.SendAsync("dataChanged", new { kind = "task" });
```

Trình duyệt nghe `dataChanged` → tự tải lại phần dữ liệu liên quan (Kanban/List/Lịch).

### 4.6. Hub realtime — `RealtimeHub`

```csharp
[Authorize]                  // bắt buộc đăng nhập mới kết nối được
public class RealtimeHub : Hub { }   // chỉ nhận push, không cần method client gọi lên
```

Đăng ký endpoint trong `Program.cs`: `app.MapHub<RealtimeHub>("/hubs/realtime");`

### 4.7. Khi nào notification/log được tạo?

| Hành động | Notification | Activity Log |
|-----------|:------------:|:------------:|
| Tạo task (giao người khác) | ✅ (người được giao) | ✅ Created |
| Sửa task (giao người mới) | ✅ (chỉ người mới) | ✅ Updated |
| Đổi trạng thái | — | ✅ ChangedStatus |
| Bình luận | — | ✅ Commented |
| Xếp lịch | — | ✅ Scheduled |
| Tạo/sửa project | — | ✅ Created/Updated |

---

## 5. Câu hỏi thường gặp khi bảo vệ (Q&A)

**Q: Realtime hoạt động thế nào, không phải reload trang à?**
A: Dùng **SignalR (WebSocket)**. Server gọi `Clients.User(...).SendAsync("notify", ...)`
đẩy thẳng xuống trình duyệt; JS nghe sự kiện và cập nhật badge/toast ngay.

**Q: Notification gửi đúng người bằng cách nào?**
A: `Clients.User(userId.ToString())` — SignalR ánh xạ theo `NameIdentifier` claim của
người đăng nhập, nên đúng người dù họ mở nhiều tab.

**Q: Activity Log thiết kế sao mà ghi được nhiều loại đối tượng?**
A: Bảng tổng quát với `EntityType` + `EntityId` + `Description`. Một bảng ghi log cho
Task, Project… mà không cần mỗi loại một bảng.

**Q: Notification và Activity Log có trùng vai trò không?**
A: Không. Notification là để **báo cho người nhận** (có đã đọc/chưa đọc); Activity Log
để **theo dõi lịch sử thao tác**, phục vụ quản lý.

**Q: Vì sao sửa task chỉ báo cho người mới được giao?**
A: So sánh danh sách cũ (`previousAssignees`) và mới, chỉ thông báo phần chênh lệch —
tránh làm phiền người đã được giao từ trước.

**Q: `ExecuteUpdateAsync` lợi gì so với load rồi sửa?**
A: Sinh **một câu UPDATE** chạy thẳng dưới DB, không cần kéo entity lên bộ nhớ — nhanh
và ít tốn tài nguyên khi đánh dấu nhiều thông báo cùng lúc.
