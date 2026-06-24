# Chức năng 7 — Quản lý Task (Công việc) & Giao việc đa phụ trách (Many-to-Many)

## 1. Mục tiêu

Quản lý **công việc (task)** trong project: tạo/sửa/xoá, đặt deadline, độ ưu tiên,
trạng thái, và **giao cho nhiều người cùng làm** (đa phụ trách). Xem dưới nhiều dạng:
**List**, **Kanban 3 cột**, và **Lịch** (xem Chức năng 8).

---

## 2. Khái niệm cần nắm để giải thích

### 2.1. Vì sao đặt tên `TaskItem` chứ không `Task`?

Trong .NET, `System.Threading.Tasks.Task` là kiểu có sẵn cho lập trình bất đồng bộ.
Đặt tên entity là **`TaskItem`** để **tránh trùng tên** gây nhầm lẫn.

### 2.2. Quan hệ Nhiều-Nhiều (Many-to-Many) — đa phụ trách

Một **task** giao cho nhiều **người**, một **người** nhận nhiều **task** → quan hệ
nhiều-nhiều, hiện thực bằng **bảng nối** `TaskAssignee`:

```
TaskItem (n) ──< TaskAssignee >── (n) User
```

`TaskAssignee` có **khóa chính kép** `(TaskItemId, UserId)` → mỗi người chỉ xuất hiện
**một lần** trong một task.

```csharp
modelBuilder.Entity<TaskAssignee>()
    .HasKey(ta => new { ta.TaskItemId, ta.UserId });   // khóa chính kép
```

### 2.3. Các thuộc tính chính của task

| Thuộc tính | Ý nghĩa |
|------------|---------|
| `Priority` | Low / Medium / High |
| `Status` | Todo / Doing / Done (3 cột Kanban) |
| `DueDate` | Hạn chót |
| `ScheduledStart` + `DurationMinutes` | Xếp lịch trên Timeline (Chức năng 8) |
| `Assignees` | Danh sách người phụ trách (đa phụ trách) |
| `IsOverdue` | Quá hạn = có deadline + chưa Done + deadline đã qua |

---

## 3. Luồng hoạt động

```
TasksController.Index   → danh sách (lọc theo project, status, từ khoá, người)
TasksController.Board   → Kanban 3 cột (Todo/Doing/Done)
TasksController.Create  → TaskService.CreateAsync  → tạo task + gán người + thông báo + ghi log
TasksController.Edit    → TaskService.UpdateAsync  → đồng bộ danh sách phụ trách + thông báo người mới
ChangeStatus / Delete / AddComment ...
```

---

## 4. Các file liên quan

| File | Vai trò |
|------|---------|
| [Controllers/TasksController.cs](../Controllers/TasksController.cs) | Action List/Board/Create/Edit/ChangeStatus/Delete… |
| [Services/TaskService.cs](../Services/TaskService.cs) | Toàn bộ nghiệp vụ task. |
| [Models/TaskItem.cs](../Models/TaskItem.cs) | Entity task. |
| [Models/TaskAssignee.cs](../Models/TaskAssignee.cs) | **Bảng nối nhiều-nhiều** task ↔ user. |
| [Data/AppDbContext.cs](../Data/AppDbContext.cs) | Cấu hình khóa chính kép + cascade/restrict. |
| [ViewModels/TaskViewModels.cs](../ViewModels/TaskViewModels.cs) | Form + List + Kanban view model. |
| [Views/Tasks/](../Views/Tasks/) | Index, Board, Create, Edit, Details, _Form. |

---

## 5. Giải thích code chính

### 5.1. Quyền thấy task — `Accessible` (kết hợp workspace + đội)

```csharp
private IQueryable<TaskItem> Accessible(int userId, bool seeAll)
{
    var query = _db.Tasks.AsQueryable();
    if (seeAll) return query;   // SuperAdmin

    return query.Where(t =>
        t.Project.Workspace.Members.Any(m => m.UserId == userId)   // việc trong workspace mình tham gia
        || t.Assignees.Any(a =>                                    // HOẶC giao cho mình / người trong đội mình
                a.UserId == userId
                || a.User.ManagerId == userId
                || (a.User.Manager != null && a.User.Manager.ManagerId == userId)));
}
```

> Với đa phụ trách: chỉ cần **một** người phụ trách thoả điều kiện là task hiển thị.
> Đây là chỗ phân quyền (Chức năng 4) gặp dữ liệu task.

### 5.2. Tạo task + gán nhiều người — `CreateAsync`

```csharp
// Chỉ chấp nhận người thực hiện LÀ THÀNH VIÊN PROJECT (lọc + loại trùng)
var assigneeIds = await ValidAssigneesAsync(model.ProjectId, model.AssigneeIds);

// Mặc định: không chọn ai mà người tạo là thành viên project → giao cho chính họ
if (assigneeIds.Count == 0 &&
    await _db.ProjectMembers.AnyAsync(m => m.ProjectId == model.ProjectId && m.UserId == userId))
    assigneeIds.Add(userId);

var task = new TaskItem
{
    Title = model.Title.Trim(), ProjectId = model.ProjectId,
    Priority = model.Priority, Status = model.Status, DueDate = model.DueDate,
    Assignees = assigneeIds.Select(uid => new TaskAssignee { UserId = uid }).ToList()  // ← gán nhiều người
};
_db.Tasks.Add(task);
await _db.SaveChangesAsync();

await _activity.LogAsync(userId, "Created", "Task", task.Id, $"Tạo task \"{task.Title}\""); // nhật ký
foreach (var uid in assigneeIds.Where(id => id != userId))
    await _notifications.CreateAsync(uid, $"Bạn được giao task: {task.Title}", task.Id);   // thông báo
await BroadcastChangedAsync();                                                              // realtime đồng bộ
```

### 5.3. Chỉ giao cho thành viên project — `ValidAssigneesAsync`

```csharp
private async Task<List<int>> ValidAssigneesAsync(int projectId, IEnumerable<int>? requested)
{
    var wanted = requested.Distinct().ToHashSet();
    return await _db.ProjectMembers
        .Where(m => m.ProjectId == projectId && wanted.Contains(m.UserId))  // lọc đúng thành viên project
        .Select(m => m.UserId).ToListAsync();
}
```

> An toàn nghiệp vụ: dù form bị sửa, **không thể gán người ngoài project** vào task.

### 5.4. Sửa task — đồng bộ danh sách phụ trách + thông báo người MỚI

```csharp
var previousAssignees = task.Assignees.Select(a => a.UserId).ToHashSet();
var newAssignees = await ValidAssigneesAsync(task.ProjectId, model.AssigneeIds);

task.Assignees.Clear();                          // gỡ hết
foreach (var uid in newAssignees)                // gán lại theo danh sách mới
    task.Assignees.Add(new TaskAssignee { TaskItemId = task.Id, UserId = uid });
await _db.SaveChangesAsync();

// Chỉ thông báo cho người MỚI được giao (không phiền người đã có)
foreach (var uid in newAssignees.Where(id => !previousAssignees.Contains(id)))
    await _notifications.CreateAsync(uid, $"Bạn được giao task: {task.Title}", task.Id);
```

### 5.5. Đổi trạng thái nhanh (Kanban) — `ChangeStatusAsync`

```csharp
var oldStatus = task.Status;
task.Status = status;
await _db.SaveChangesAsync();
await _activity.LogAsync(userId, "ChangedStatus", "Task", task.Id,
    $"Đổi trạng thái task \"{task.Title}\": {oldStatus.Label()} → {status.Label()}");
```

### 5.6. Tải thành viên project bằng AJAX cho form

Khi chọn project trong form tạo task, JS gọi `TasksController.ProjectMembers` để **đổ
ngay** danh sách người thực hiện mà không phải lưu trước:

```csharp
var members = await _tasks.GetProjectMembersAsync(projectId);
return Json(members.Select(u => new { id = u.Id, name = u.FullName }));
```

### 5.7. Cascade khi xoá task / xoá user

```csharp
// Xoá task → gỡ luôn các phân công của nó
.HasOne(ta => ta.TaskItem).WithMany(t => t.Assignees).OnDelete(DeleteBehavior.Cascade);
// Xoá user → KHÔNG cascade (tránh nhiều đường cascade); gỡ thủ công khi xoá user
.HasOne(ta => ta.User).WithMany(u => u.TaskAssignments).OnDelete(DeleteBehavior.Restrict);
```

---

## 6. Câu hỏi thường gặp khi bảo vệ (Q&A)

**Q: Quan hệ nhiều-nhiều hiện thực thế nào?**
A: Qua bảng nối `TaskAssignee` có **khóa chính kép** `(TaskItemId, UserId)`. EF Core
cấu hình bằng `HasKey(ta => new { ta.TaskItemId, ta.UserId })`.

**Q: Vì sao không dùng many-to-many tự động của EF Core?**
A: Có bảng nối tường minh dễ kiểm soát (đặt cascade/restrict riêng, sau này thêm cột
như "vai trò trong task" cũng dễ) và **dễ giải thích** trong báo cáo.

**Q: Sao đặt tên `TaskItem`?**
A: Tránh trùng `System.Threading.Tasks.Task` của .NET.

**Q: Giao việc cho người ngoài project được không?**
A: Không. `ValidAssigneesAsync` lọc chỉ giữ lại **thành viên project**, dù request có
gửi id khác.

**Q: Một task giao cho 3 người thì hiển thị thế nào trên lịch?**
A: Task chung đó xuất hiện trên **lịch của cả 3 người** — vì lịch là của từng người
(xem Chức năng 8). Nhiều người làm cùng khung giờ là bình thường.

**Q: Đổi trạng thái trên Kanban hoạt động sao?**
A: Mỗi thẻ có dropdown trạng thái; chọn xong POST tới `ChangeStatus`, Service đổi
`Status`, ghi nhật ký và đẩy realtime để các màn khác tự cập nhật.
