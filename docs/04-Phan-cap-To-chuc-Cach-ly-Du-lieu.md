# Chức năng 4 — Phân cấp tổ chức & Cách ly dữ liệu theo đội (Hierarchy & Isolation)

## 1. Mục tiêu

Mô phỏng **cơ cấu một công ty** với 4 cấp và đảm bảo mỗi người **chỉ thấy/quản lý
phần dữ liệu thuộc đội của mình** — không thấy đội khác.

```
SuperAdmin (3)  →  Admin (2)  →  Manager (1)  →  User (0)
   chủ hệ thống    quản trị      trưởng nhóm     nhân viên
```

Đây là điểm nhấn "vượt trên CRUD cơ bản" của đồ án.

---

## 2. Khái niệm cần nắm để giải thích

### 2.1. Hai trục phân quyền

Dự án phân quyền theo **2 trục kết hợp**:

| Trục | Câu hỏi | Lưu ở đâu |
|------|---------|-----------|
| **Cấp bậc (Role)** | Bạn là cấp gì? | `User.RoleId` → claim `Role` |
| **Quan hệ trực thuộc (Hierarchy)** | Ai là cấp trên của bạn? | `User.ManagerId` (tự tham chiếu) |

Một người vừa phải **đủ cấp**, vừa phải **nằm trong đội** thì mới quản lý được người khác.

### 2.2. Quan hệ tự tham chiếu (self-reference)

`User.ManagerId` trỏ tới chính bảng `User` → tạo cây tổ chức. Người tạo tài khoản tự
trở thành **cấp trên trực tiếp** (`ManagerId`) của người được tạo.

```
SuperAdmin
   └── Admin (Kỹ thuật)
         └── Manager (Dev)
               ├── User A
               └── User B
```

### 2.3. Cách ly dữ liệu (Data Isolation)

- **SuperAdmin**: thấy **tất cả** (chủ hệ thống).
- **Admin/Manager**: chỉ thấy **đội của mình**, sâu **tối đa 2 cấp** (Admin thấy các
  Manager + User của chính các Manager đó). Hai Admin **không thấy đội của nhau**.
- **User**: chỉ thấy phần của mình.

### 2.4. Tài khoản Cá nhân vs Nhân viên công ty (`AccountType`)

| Loại | Tạo bởi | Thuộc cơ cấu? |
|------|---------|----------------|
| **Personal** (Cá nhân) | Tự đăng ký / đăng nhập Google | Không (ManagerId = null) |
| **Company** (Nhân viên) | Cấp quản lý tạo | Có (ManagerId = người tạo) |

Bộ chọn "xem theo người" và danh sách ứng viên thành viên **chỉ tính nhân viên công ty**.

---

## 3. Các file liên quan

| File | Vai trò |
|------|---------|
| [Models/Roles.cs](../Models/Roles.cs) | Hằng số vai trò + `Level` (cấp bậc) + `AssignableBy` (quyền gán) — **gom 1 chỗ, không hardcode**. |
| [Models/User.cs](../Models/User.cs) | `RoleId`, `ManagerId` (tự tham chiếu), `AccountType`. |
| [Services/UserService.cs](../Services/UserService.cs) | `VisibleUsers`, `CanManage`, `ResolveScopeAsync` — trái tim phân cấp. |
| [Controllers/BaseController.cs](../Controllers/BaseController.cs) | `CurrentRole`, `CanSeeAllData`, `IsPersonalAccount` từ claims. |
| [Data/AppDbContext.cs](../Data/AppDbContext.cs) | Quan hệ `Manager` self-reference dùng `Restrict`. |

---

## 4. Giải thích code chính

### 4.1. Gom logic vai trò một chỗ — `Roles.cs`

```csharp
public static int Level(string? role) => role switch
{
    SuperAdmin => 3, Admin => 2, Manager => 1, _ => 0   // số lớn = quyền cao
};

public static bool CanSeeAllData(string? role) => role == SuperAdmin; // chỉ SA thấy tất cả

// Một người chỉ được gán cho người khác các vai trò THẤP HƠN cấp của mình
public static IEnumerable<string> AssignableBy(string? viewerRole)
{
    int max = Level(viewerRole);
    return All.Where(r => Level(r) < max);
}
```

> Điểm cần nhấn: **không hardcode** chuỗi "Admin"/"Manager" rải rác trong code — tất cả
> tập trung ở `Roles.cs`, dễ bảo trì và dễ giải thích.

### 4.2. Phạm vi nhìn thấy — `VisibleUsers` (cách ly dữ liệu)

```csharp
private IQueryable<User> VisibleUsers(int viewerId, string viewerRole)
{
    var query = _db.Users.Include(u => u.Role).Include(u => u.Manager);
    if (Roles.CanSeeAllData(viewerRole))
        return query;                          // SuperAdmin: tất cả

    return query.Where(u =>
        u.Id == viewerId                       // chính mình
        || u.ManagerId == viewerId             // cấp dưới trực tiếp
        || (u.Manager != null && u.Manager.ManagerId == viewerId)); // cấp dưới của cấp dưới (2 tầng)
}
```

Đây chính là **cách ly dữ liệu**: mọi truy vấn người dùng đều đi qua bộ lọc này.

### 4.3. Quyền quản lý một người cụ thể — `CanManage`

```csharp
private static bool CanManage(int viewerId, string viewerRole, User target)
{
    if (target.Id == viewerId) return false;                          // không quản lý chính mình
    if (Roles.Level(target.Role.Name) >= Roles.Level(viewerRole)) return false; // không quản người ngang/cao hơn
    if (Roles.CanSeeAllData(viewerRole)) return true;                 // SuperAdmin: toàn quyền cấp dưới

    return target.ManagerId == viewerId                               // trong đội mình
        || (target.Manager != null && target.Manager.ManagerId == viewerId);
}
```

> Ba ràng buộc an toàn rút ra từ đây: **không tự quản lý mình**, **không đụng người ngang
> hoặc cao cấp hơn** (hai Admin không xoá được nhau), và phải **trong đội**.

### 4.4. Người tạo trở thành cấp trên — `CreateAsync`

```csharp
var user = new User
{
    ...
    RoleId = role.Id,
    ManagerId = creatorId,                 // ← người tạo là cấp trên trực tiếp
    AccountType = AccountType.Company,     // do cấp quản lý tạo = nhân viên công ty
};
```

Form tạo người dùng **không cần chọn "người quản lý"** — tự suy ra từ người đang đăng nhập.

### 4.5. "Xem theo người" — `ResolveScopeAsync`

Cho phép Manager trở lên lọc dữ liệu theo từng nhân viên hoặc xem "Tất cả" trong đội:

```csharp
bool canViewOthers = Roles.Level(viewerRole) >= Roles.Level(Roles.Manager) && visible.Count > 1;

int selected;
if (!canViewOthers)        selected = viewerId;   // luôn là chính mình
else if (hợp lệ id)        selected = id;         // một người cụ thể
else                       selected = 0;          // "Tất cả"

return new EmployeeScopeResult {
    Visible = visible, SelectedId = selected, CanViewOthers = canViewOthers,
    AssigneeFilter = selected == 0 ? null : new List<int> { selected }
};
```

`AssigneeFilter` này được truyền xuống truy vấn task/lịch để lọc đúng phạm vi.

### 4.6. Xoá Manager an toàn — gỡ liên kết tự tham chiếu trước

Vì quan hệ `Manager` dùng `Restrict` (tránh vòng cascade), khi xoá một người là cấp trên
của người khác, phải **gỡ liên kết** trước:

```csharp
await _db.Users.Where(u => u.ManagerId == targetUserId)
    .ExecuteUpdateAsync(s => s.SetProperty(u => u.ManagerId, (int?)null));
```

---

## 5. Bảng tổng hợp quyền (rất hợp để đưa lên slide)

| Hành động | SuperAdmin | Admin | Manager | User |
|-----------|:---------:|:-----:|:-------:|:----:|
| Xem **toàn bộ** dữ liệu hệ thống | ✅ | ❌ | ❌ | ❌ |
| Tạo/quản lý cấp ngay dưới trong đội | ✅ | ✅ (Manager) | ✅ (User) | ❌ |
| Cấp/bỏ quyền Admin | ✅ | ❌ | ❌ | ❌ |
| Xem Dashboard | ✅ | ✅ | ❌ | ❌ |
| Bộ chọn "xem theo người" | ✅ | ✅ | ✅ | ❌ |
| Bị xoá | ❌ (bất khả xoá) | chỉ SA | cấp trên | cấp trên |

---

## 6. Câu hỏi thường gặp khi bảo vệ (Q&A)

**Q: Phân quyền của em có gì hơn CRUD bình thường?**
A: Em kết hợp **2 trục**: cấp bậc (Role) và quan hệ trực thuộc (ManagerId tự tham chiếu),
nên dữ liệu **cách ly theo đội** — hai Admin không thấy đội của nhau, đúng như công ty thật.

**Q: "Sâu tối đa 2 cấp" nghĩa là gì?**
A: Admin thấy Manager (cấp dưới trực tiếp) **và** User của các Manager đó (cấp dưới gián
tiếp 1 tầng) — tổng 2 tầng dưới. Thể hiện bằng điều kiện `u.Manager.ManagerId == viewerId`.

**Q: Vì sao quan hệ Manager dùng `Restrict` chứ không `Cascade`?**
A: Để tránh **multiple cascade paths** của SQL Server (lỗi khi nhiều đường xoá cùng trỏ
về một bảng). Khi xoá Manager, code chủ động set `ManagerId = null` cho nhân viên trước.

**Q: Tài khoản tự đăng ký nằm ở đâu trong cây?**
A: Là **User độc lập** — `AccountType = Personal`, `ManagerId = null`, không thuộc đội ai,
chỉ tự quản lý việc cá nhân.

**Q: Tại sao tách Personal/Company?**
A: Để chuẩn hoá theo mô hình công ty: bộ chọn "xem theo người", danh sách thành viên
chỉ tính **nhân viên công ty**, còn người dùng cá nhân có giao diện gọn riêng.

**Q: SuperAdmin có thể bị xoá không?**
A: Không, **bất khả xoá** dưới mọi hình thức; `DbSeeder` luôn đảm bảo tồn tại đúng một
SuperAdmin khi khởi động.
