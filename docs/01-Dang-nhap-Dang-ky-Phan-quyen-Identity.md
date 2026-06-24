# Chức năng 1 — Đăng nhập, Đăng ký & Phân quyền Cookie Claims (ASP.NET Core Identity)

## 1. Mục tiêu

- Cho phép người dùng **đăng ký** tài khoản, **đăng nhập**, **đăng xuất**.
- Sau khi đăng nhập, hệ thống phát một **cookie xác thực** chứa thông tin người dùng
  (id, tên, vai trò, loại tài khoản) dưới dạng **claims**.
- Dựa vào claims để **phân quyền**: ai được vào trang nào, được thấy/sửa dữ liệu nào.

Tất cả dựa trên **ASP.NET Core Identity** — framework xác thực/uỷ quyền chính thức của .NET.

---

## 2. Khái niệm cần nắm để giải thích

### 2.1. ASP.NET Core Identity là gì?

Là **bộ thư viện sẵn có của Microsoft** lo toàn bộ phần "tài khoản người dùng":
lưu user/role, băm mật khẩu, kiểm tra đăng nhập, phát hành cookie, đăng nhập ngoài
(Google), sinh token... Mình **không phải tự viết** phần băm mật khẩu hay quản lý cookie.

Hai lớp trung tâm của Identity mà code dùng:

| Lớp | Vai trò |
|-----|---------|
| `UserManager<User>` | Thao tác với user: tạo, tìm theo email, đổi mật khẩu, kiểm tra... |
| `SignInManager<User>` | Đăng nhập/đăng xuất: kiểm tra mật khẩu rồi phát cookie. |

### 2.2. Claim là gì? Cookie Claims là gì?

- **Claim** = một "mẩu thông tin" về người dùng, dạng *(loại, giá trị)*.
  Ví dụ: `(Name, "Lê Tấn Tài")`, `(Role, "Admin")`, `(NameIdentifier, "5")`.
- Sau khi đăng nhập thành công, Identity gom các claim này, **mã hoá + ký** rồi
  bỏ vào **cookie** gửi về trình duyệt. Mỗi request sau, trình duyệt tự gửi cookie lên,
  server giải mã ra claims → biết "ai đang đăng nhập, vai trò gì" mà **không cần truy
  vấn database lại**.
- Đây gọi là xác thực **stateless bằng cookie claims**.

### 2.3. Điểm đặc biệt trong dự án này

Dự án dùng Identity với **khóa kiểu `int`** (mặc định Identity dùng `string`/GUID):

```csharp
public class User : IdentityUser<int> { ... }   // Models/User.cs
public class Role : IdentityRole<int> { ... }
public class AppDbContext : IdentityDbContext<User, Role, int> { ... }
```

Lý do: giữ nguyên mọi khóa ngoại `int` trong hệ thống (Workspace.OwnerId, Task… đều `int`).

Và mỗi user có **đúng 1 vai trò** qua FK trực tiếp `User.RoleId` (thay vì bảng nối
`AspNetUserRoles` nhiều-nhiều) cho gọn và dễ giải thích.

---

## 3. Luồng hoạt động

### Đăng ký

```
Người dùng nhập form đăng ký (họ tên, email, mật khẩu)
   → AccountController.Register (POST)
   → AuthService.RegisterAsync: kiểm tra email trùng → tạo User (vai trò "User", loại Personal)
   → UserManager.CreateAsync(user, password)  ← Identity tự BĂM mật khẩu
   → SignInManager.SignInAsync(user)           ← phát cookie, đăng nhập luôn
   → chuyển vào trang chính
```

### Đăng nhập

```
Người dùng nhập email + mật khẩu
   → AccountController.Login (POST)
   → UserManager.FindByEmailAsync(email)       ← tìm user
   → SignInManager.PasswordSignInAsync(...)     ← so khớp mật khẩu đã băm, phát cookie
   → đúng: chuyển vào trang chính | sai: báo "Email hoặc mật khẩu không đúng"
```

### Cookie được tạo claims như thế nào?

`SignInManager` gọi **`AppUserClaimsPrincipalFactory`** để sinh các claim đặt vào cookie:

```
Name           = họ tên đầy đủ (FullName)
Role           = tên vai trò lấy theo User.RoleId
account_type   = Personal / Company
NameIdentifier = User.Id   (Identity tự thêm)
```

---

## 4. Các file liên quan

| File | Vai trò |
|------|---------|
| [Program.cs](../Program.cs) | Đăng ký Identity, cấu hình mật khẩu, đường dẫn cookie. |
| [Models/User.cs](../Models/User.cs) | Entity User kế thừa `IdentityUser<int>` + trường nghiệp vụ. |
| [Models/Role.cs](../Models/Role.cs) | Entity Role kế thừa `IdentityRole<int>`. |
| [Controllers/AccountController.cs](../Controllers/AccountController.cs) | Action Login/Register/Logout. |
| [Services/AuthService.cs](../Services/AuthService.cs) | Nghiệp vụ đăng ký, tạo tài khoản. |
| [Services/AppUserClaimsPrincipalFactory.cs](../Services/AppUserClaimsPrincipalFactory.cs) | Sinh claims đặt vào cookie. |
| [Services/PasswordHasher.cs](../Services/PasswordHasher.cs) | Băm mật khẩu PBKDF2. |
| [Controllers/BaseController.cs](../Controllers/BaseController.cs) | Đọc claims ra `CurrentUserId`, `CurrentRole`. |
| [Models/AppClaims.cs](../Models/AppClaims.cs) | Hằng số tên claim tùy biến (`account_type`). |

---

## 5. Giải thích code chính

### 5.1. Đăng ký Identity trong `Program.cs`

```csharp
builder.Services.AddIdentity<User, Role>(options =>
{
    options.Password.RequiredLength = 6;        // mật khẩu tối thiểu 6 ký tự
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;     // email không trùng
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>()       // lưu user/role bằng EF Core
.AddDefaultTokenProviders();                    // sinh token (đổi mật khẩu...)
```

Và cấu hình **cookie đăng nhập**:

```csharp
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";        // chưa đăng nhập → đưa về đây
    options.AccessDeniedPath = "/Home/Index";    // không đủ quyền → đưa về đây
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;            // còn hoạt động thì gia hạn cookie
});
```

### 5.2. Đăng nhập — `AccountController.Login` (POST)

```csharp
var user = await _userManager.FindByEmailAsync(model.Email.Trim());
if (user is not null)
{
    var result = await _signInManager.PasswordSignInAsync(
        user, model.Password, isPersistent: true, lockoutOnFailure: false);
    if (result.Succeeded)
        return Redirect(url);   // đăng nhập OK → phát cookie + chuyển trang
}
// sai email hoặc mật khẩu → báo lỗi chung (không nói rõ sai cái nào, an toàn hơn)
```

> Điểm cần nhấn khi bảo vệ: báo lỗi **chung chung** "Email hoặc mật khẩu không đúng"
> để kẻ xấu không dò được email nào tồn tại.

### 5.3. Đăng ký — `AuthService.RegisterAsync`

```csharp
if (await _userManager.FindByEmailAsync(email) is not null)
    return (false, "Email đã được sử dụng.", null);

var user = new User
{
    FullName = model.FullName.Trim(),
    Email = email, UserName = email,
    RoleId = userRole.Id,                 // vai trò mặc định "User"
    AccountType = AccountType.Personal     // tự đăng ký = tài khoản cá nhân
};
var result = await _userManager.CreateAsync(user, model.Password); // Identity tự băm
```

### 5.4. Sinh claims cho cookie — `AppUserClaimsPrincipalFactory`

Đây là chỗ **biến User thành claims** đặt vào cookie:

```csharp
public override async Task<ClaimsPrincipal> CreateAsync(User user)
{
    var principal = await base.CreateAsync(user);
    var identity = (ClaimsIdentity)principal.Identity!;

    // Đổi claim Name (mặc định = email) thành họ tên đầy đủ
    identity.AddClaim(new Claim(ClaimTypes.Name, user.FullName));

    // Thêm claim Role theo FK trực tiếp RoleId
    var role = await _roleManager.FindByIdAsync(user.RoleId.ToString());
    identity.AddClaim(new Claim(ClaimTypes.Role, role.Name));

    // Loại tài khoản để giao diện điều chỉnh mà không cần hỏi lại DB
    identity.AddClaim(new Claim(AppClaims.AccountType, user.AccountType.ToString()));
    return principal;
}
```

### 5.5. Đọc claims để phân quyền — `BaseController`

Mọi controller kế thừa `BaseController` để lấy nhanh thông tin người đăng nhập từ cookie:

```csharp
protected int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
protected string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? Roles.User;
protected bool CanSeeAllData => Roles.CanSeeAllData(CurrentRole); // chỉ SuperAdmin
```

### 5.6. Phân quyền bằng attribute

- `[Authorize]` đặt trên controller → **bắt buộc đăng nhập** mới vào được
  (chưa đăng nhập sẽ tự chuyển về `/Account/Login`).
- `[Authorize(Roles = "SuperAdmin,Admin")]` → chỉ các vai trò đó vào được (vd Dashboard).

### 5.7. Băm mật khẩu — `PasswordHasher` (PBKDF2)

Mật khẩu **không bao giờ lưu dạng chữ thường**. Dự án băm bằng **PBKDF2 (SHA-256, 100k
vòng, salt ngẫu nhiên)**. Lớp `PasswordHasher` cài cả interface `IPasswordHasher<User>`
của Identity để `UserManager`/`SignInManager` dùng đúng thuật toán này.

---

## 6. Câu hỏi thường gặp khi bảo vệ (Q&A)

**Q: Vì sao dùng Identity mà không tự viết đăng nhập?**
A: Identity là chuẩn của Microsoft, lo sẵn băm mật khẩu an toàn, quản lý cookie, chống
các lỗi bảo mật phổ biến. Tự viết dễ sai và mất thời gian.

**Q: Cookie chứa mật khẩu à?**
A: Không. Cookie chỉ chứa **claims** (id, tên, vai trò...) đã được **ký + mã hoá**.
Không có mật khẩu trong cookie. Server giải mã cookie để biết ai đang đăng nhập.

**Q: Phân quyền hoạt động thế nào?**
A: Khi đăng nhập, vai trò được nhét vào claim `Role` trong cookie. `[Authorize(Roles=...)]`
đọc claim này để chặn/cho phép. Sâu hơn thì Service kiểm tra theo đội (xem Chức năng 4).

**Q: Vì sao một user chỉ 1 vai trò qua `RoleId` mà không dùng bảng nối Identity?**
A: Mô hình công ty của mình mỗi người **đúng một chức vụ**, nên FK trực tiếp đơn giản,
truy vấn nhanh và dễ giải thích hơn quan hệ nhiều-nhiều.

**Q: `isPersistent: true` nghĩa là gì?**
A: Cookie được lưu lâu dài (7 ngày), đóng trình duyệt mở lại vẫn còn đăng nhập, thay
vì hết khi tắt trình duyệt.

**Q: Mật khẩu băm bằng gì? Có giải ngược được không?**
A: PBKDF2-SHA256 với salt ngẫu nhiên + 100.000 vòng lặp. Đây là hàm băm một chiều,
**không giải ngược được**; khi đăng nhập ta băm lại mật khẩu nhập vào rồi so sánh.
