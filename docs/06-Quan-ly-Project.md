# Chức năng 6 — Quản lý Project (Dự án) & Phân bổ thành viên (Tiến độ & Members)

## 1. Mục tiêu

**Project** nằm trong một workspace, chứa các **task** và **page (ghi chú)**. Chức năng:

- CRUD project.
- **Thanh tiến độ** theo % task hoàn thành.
- **Phân bổ thành viên**: lấy người *đã ở trong workspace* bố trí vào project.

---

## 2. Khái niệm cần nắm để giải thích

### 2.1. Quan hệ phân cấp dữ liệu

```
Workspace (1) ──< Project (n) ──< Task (n)
                          └─────< Page (n)
                          └─────< ProjectMember (n)  ← nối với User
```

Một project chỉ thuộc **một** workspace. Thành viên project là **tập con** của thành viên
workspace (phải vào workspace trước, rồi mới bố trí vào project).

### 2.2. Tiến độ (Progress) tính thế nào?

```
% hoàn thành = số task Done / tổng số task × 100
```

Tính ngay trong Service khi load chi tiết project.

### 2.3. Ai được quản lý thành viên project?

```csharp
canManage = SuperAdmin
         || chủ workspace chứa project
         || Owner của chính project đó
```

---

## 3. Các file liên quan

| File | Vai trò |
|------|---------|
| [Controllers/ProjectsController.cs](../Controllers/ProjectsController.cs) | Action CRUD + add/remove member. |
| [Services/ProjectService.cs](../Services/ProjectService.cs) | Nghiệp vụ project + tính tiến độ. |
| [Models/Project.cs](../Models/Project.cs) | Entity project. |
| [Models/ProjectMember.cs](../Models/ProjectMember.cs) | Bảng nối user ↔ project + `MemberRole`. |
| [ViewModels/ProjectViewModels.cs](../ViewModels/ProjectViewModels.cs) / [ProjectDetailsViewModel.cs](../ViewModels/ProjectDetailsViewModel.cs) | View model form + chi tiết. |
| [Views/Projects/](../Views/Projects/) | Index, Details, Create, Edit, _Form. |

---

## 4. Giải thích code chính

### 4.1. Lọc project truy cập được — `Accessible`

```csharp
private IQueryable<Project> Accessible(int userId, bool seeAll)
{
    var query = _db.Projects.AsQueryable();
    return seeAll ? query
                  : query.Where(p => p.Workspace.Members.Any(m => m.UserId == userId));
}
```

> Quyền vào project = **là thành viên workspace chứa nó** (hoặc SuperAdmin). Đơn giản,
> nhất quán với cách ly dữ liệu chung.

### 4.2. Tính tiến độ + dữ liệu chi tiết — `GetDetailsAsync`

```csharp
return new ProjectDetailsViewModel
{
    Project = project,
    Pages   = project.Pages.OrderByDescending(p => p.UpdatedAt).ToList(),
    Tasks   = project.Tasks.OrderBy(t => t.Status).ThenBy(t => t.DueDate).ToList(),
    Members = project.Members.OrderByDescending(m => m.Role).ToList(),
    RecentActivities = activities,                 // nhật ký liên quan project + task của nó
    AddableUsers = addable,                        // người có thể thêm vào project
    CanManageMembers = canManage,
    TotalTasks     = project.Tasks.Count,
    CompletedTasks = project.Tasks.Count(t => t.Status == TaskStatus.Done)  // ← cho thanh tiến độ
};
```

View vẽ thanh tiến độ từ `CompletedTasks / TotalTasks`.

### 4.3. Tạo project — người tạo thành Owner

```csharp
if (!await HasWorkspaceAccessAsync(model.WorkspaceId, userId, seeAll)) return null; // phải có quyền workspace

var project = new Project { Name = ..., WorkspaceId = model.WorkspaceId };
project.Members.Add(new ProjectMember { UserId = userId, Role = MemberRole.Owner });
_db.Projects.Add(project);
await _db.SaveChangesAsync();

await _activity.LogAsync(userId, "Created", "Project", project.Id, $"Tạo project \"{project.Name}\""); // ghi nhật ký
```

### 4.4. Ứng viên thêm vào project = thành viên workspace chưa có trong project

```csharp
var memberIds = project.Members.Select(m => m.UserId).ToHashSet();
addable = await _db.WorkspaceMembers
    .Where(m => m.WorkspaceId == project.WorkspaceId && !memberIds.Contains(m.UserId))
    .Select(m => m.User)
    .OrderBy(u => u.FullName)
    .ToListAsync();
```

### 4.5. Thêm thành viên — phải đã ở trong workspace

```csharp
bool inWorkspace = await _db.WorkspaceMembers
    .AnyAsync(m => m.WorkspaceId == project.WorkspaceId && m.UserId == targetUserId);
if (!inWorkspace) return false;   // ← không bố trí người ngoài workspace vào project
```

### 4.6. Không gỡ Owner của project

```csharp
if (member.Role == MemberRole.Owner) return false; // Owner project là cố định
```

---

## 5. Quan hệ với các chức năng khác

- **Task (Chức năng 7)** chỉ giao được cho **thành viên project** — nên phân bổ thành
  viên project là bước tiền đề để giao việc.
- **Activity Log (Chức năng 9)** ghi nhận tạo/sửa project.
- Xoá project **cascade** xoá task + page bên trong (cấu hình ở `AppDbContext`).

---

## 6. Câu hỏi thường gặp khi bảo vệ (Q&A)

**Q: Thành viên project lấy từ đâu?**
A: Chỉ từ **thành viên của workspace** chứa project — phải vào workspace trước. Code
kiểm tra `inWorkspace` trước khi thêm.

**Q: Tiến độ project tính thế nào?**
A: `CompletedTasks / TotalTasks` — đếm task `Done` chia tổng task. Tính trong Service
rồi truyền ra View vẽ thanh %.

**Q: Ai được thêm/gỡ thành viên project?**
A: SuperAdmin, **chủ workspace**, hoặc **Owner của project** đó (hàm `CanManageMembers`).

**Q: Xoá project có mất task không?**
A: Có — quan hệ `Project → Task` và `Project → Page` cấu hình **Cascade**, xoá project
xoá luôn task/page con. Đây là hành vi mong muốn.

**Q: Vì sao quyền vào project dựa trên workspace?**
A: Để nhất quán: project thuộc workspace, ai vào được workspace thì thấy project trong
đó; còn tham gia trực tiếp (ProjectMember) quyết định việc được giao task.
