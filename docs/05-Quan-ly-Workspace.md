# Chức năng 5 — Quản lý Workspace (Không gian làm việc) & Phân biệt Cá nhân / Nhóm

## 1. Mục tiêu

**Workspace** là cấp tổ chức cao nhất chứa các **project**. Mỗi workspace có:

- Một **chủ sở hữu (Owner)** + nhiều **thành viên (Member/Manager)**.
- Phân biệt **Cá nhân** (chỉ một mình) và **Nhóm** (nhiều người) để hiển thị giao diện phù hợp.

Cấu trúc dữ liệu: `Workspace → Project → (Page, Task)`.

---

## 2. Khái niệm cần nắm để giải thích

### 2.1. Quan hệ thành viên nhiều-nhiều

Một user tham gia nhiều workspace, một workspace có nhiều user → dùng **bảng nối**
`WorkspaceMember` (gồm `WorkspaceId`, `UserId`, `Role`). Có **unique index** đảm bảo
mỗi người chỉ tham gia một workspace **một lần**.

### 2.2. Hai loại vai trò khác nhau (đừng nhầm!)

| Loại vai trò | Phạm vi | Enum/định nghĩa |
|--------------|---------|------------------|
| **Vai trò hệ thống** | Toàn ứng dụng | `Roles` (SuperAdmin/Admin/Manager/User) |
| **Vai trò trong workspace** | Trong 1 workspace | `MemberRole` (Member/Manager/Owner) |

Ví dụ: một người có vai trò hệ thống là *User* nhưng có thể là *Owner* trong workspace
của chính họ.

### 2.3. Cá nhân vs Nhóm được tính thế nào?

```csharp
IsPersonal = w.Members.Count <= 1;   // chỉ 1 thành viên (chính chủ) ⇒ "Cá nhân"
```

Tài khoản loại `Personal` thấy giao diện gọn (ẩn quản lý đội ngũ, nút đổi thành
"Tạo không gian").

---

## 3. Luồng hoạt động

```
WorkspacesController.Index    → WorkspaceService.GetIndexAsync   → tách "của tôi" / "tôi tham gia"
WorkspacesController.Create   → WorkspaceService.CreateAsync     → người tạo thành Owner + thêm thành viên trong phạm vi quản lý
WorkspacesController.Details  → WorkspaceService.GetDetailsAsync → danh sách thành viên + project + người có thể thêm
                              → AddMember / RemoveMember / SetMemberRole
```

---

## 4. Các file liên quan

| File | Vai trò |
|------|---------|
| [Controllers/WorkspacesController.cs](../Controllers/WorkspacesController.cs) | Action CRUD + quản lý thành viên. |
| [Services/WorkspaceService.cs](../Services/WorkspaceService.cs) | Toàn bộ nghiệp vụ workspace. |
| [Models/Workspace.cs](../Models/Workspace.cs) | Entity workspace. |
| [Models/WorkspaceMember.cs](../Models/WorkspaceMember.cs) | Bảng nối user ↔ workspace + `MemberRole`. |
| [ViewModels/WorkspaceViewModels.cs](../ViewModels/WorkspaceViewModels.cs) | Form + card + details view model. |
| [Views/Workspaces/](../Views/Workspaces/) | Index, Details, Create, Edit, _Form, _WorkspaceCard. |

---

## 5. Giải thích code chính

### 5.1. Lọc workspace truy cập được — `Accessible`

Mọi truy vấn workspace đều đi qua bộ lọc này (cách ly dữ liệu):

```csharp
private IQueryable<Workspace> Accessible(int userId, bool seeAll)
{
    var query = _db.Workspaces.AsQueryable();
    return seeAll ? query                                  // SuperAdmin: tất cả
                  : query.Where(w => w.Members.Any(m => m.UserId == userId)); // chỉ workspace mình là thành viên
}
```

### 5.2. Trang danh sách — tách "của tôi" và "tôi tham gia"

```csharp
var cards = list.Select(w =>
{
    var mine = w.Members.FirstOrDefault(m => m.UserId == userId);
    return new WorkspaceCardViewModel
    {
        Workspace = w,
        MyRole = mine?.Role ?? MemberRole.Member,
        IsOwner = w.OwnerId == userId,
        TaskCount = w.Projects.Sum(p => p.Tasks.Count),
        IsPersonal = w.Members.Count <= 1            // ← nhãn Cá nhân / Nhóm
    };
}).ToList();

return new WorkspaceIndexViewModel
{
    Owned  = cards.Where(c => c.IsOwner).ToList(),   // "của tôi"
    Joined = cards.Where(c => !c.IsOwner).ToList(),  // "tôi tham gia"
    ViewerIsPersonal = viewer?.AccountType == AccountType.Personal
};
```

### 5.3. Tạo workspace — người tạo thành Owner

```csharp
var workspace = new Workspace { Name = ..., OwnerId = ownerId };
workspace.Members.Add(new WorkspaceMember { UserId = ownerId, Role = MemberRole.Owner }); // ← tự là Owner

// Chỉ thêm thành viên NẰM TRONG phạm vi quản lý của người tạo
var allowed = (await _users.GetVisibleEmployeesAsync(ownerId, ownerRole)).Select(u => u.Id).ToHashSet();
foreach (var uid in model.MemberIds.Distinct())
    if (uid != ownerId && allowed.Contains(uid))
        workspace.Members.Add(new WorkspaceMember { UserId = uid, Role = MemberRole.Member });
```

> Liên kết với Chức năng 4: chỉ thêm được **người trong đội mình** vào workspace.

### 5.4. Quản lý thành viên — chỉ Owner hoặc SuperAdmin

```csharp
if (ws is null || !(seeAll || ws.OwnerId == actingUserId)) return false; // chặn nếu không có quyền
```

Khi **gỡ thành viên khỏi workspace**, gỡ luôn khỏi các project bên trong (tránh dữ liệu rác):

```csharp
await _db.ProjectMembers
    .Where(pm => pm.UserId == targetUserId && pm.Project.WorkspaceId == workspaceId)
    .ExecuteDeleteAsync();
await _db.WorkspaceMembers
    .Where(m => m.WorkspaceId == workspaceId && m.UserId == targetUserId)
    .ExecuteDeleteAsync();
```

### 5.5. Không gỡ / đổi vai trò chủ sở hữu

```csharp
if (targetUserId == ws.OwnerId) return false; // Owner cố định, không gỡ / không đổi vai trò qua đây
```

### 5.6. Xoá workspace — cascade xuống project/task

`AppDbContext` cấu hình `Workspace → Project` là **Cascade**, nên xoá workspace tự xoá
toàn bộ project, page, task, member bên trong:

```csharp
modelBuilder.Entity<Project>()
    .HasOne(p => p.Workspace).WithMany(w => w.Projects)
    .HasForeignKey(p => p.WorkspaceId)
    .OnDelete(DeleteBehavior.Cascade);
```

---

## 6. Câu hỏi thường gặp khi bảo vệ (Q&A)

**Q: Workspace, Project, Task quan hệ thế nào?**
A: Workspace chứa nhiều Project, Project chứa nhiều Task và Page. Là quan hệ cha-con
1-nhiều, xoá cha thì cascade xoá con.

**Q: Phân biệt vai trò hệ thống và vai trò trong workspace?**
A: Vai trò hệ thống (`Roles`) áp toàn ứng dụng; vai trò workspace (`MemberRole`:
Member/Manager/Owner) chỉ trong phạm vi workspace đó. Một User hệ thống vẫn là Owner
workspace của mình.

**Q: "Cá nhân / Nhóm" xác định bằng gì?**
A: Đếm số thành viên — `Members.Count <= 1` là Cá nhân. Đơn giản và đúng nhu cầu.

**Q: Vì sao không cho gỡ Owner?**
A: Owner là người sở hữu workspace; gỡ Owner sẽ để lại workspace "vô chủ". Code chặn
`targetUserId == ws.OwnerId`.

**Q: Người được thêm vào workspace lấy từ đâu?**
A: Chỉ từ **nhân viên trong phạm vi quản lý** của người thao tác
(`GetVisibleEmployeesAsync`) — không thể thêm người ngoài đội.
