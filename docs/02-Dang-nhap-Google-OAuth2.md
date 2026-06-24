# Chức năng 2 — Đăng nhập bên ngoài qua Google OAuth2

## 1. Mục tiêu

Cho phép người dùng bấm **"Đăng nhập với Google"** và vào hệ thống mà **không cần
nhập mật khẩu** của ứng dụng. Lần đầu đăng nhập sẽ **tự tạo tài khoản** (loại Cá nhân);
nếu email đó đã có sẵn trong hệ thống thì **dùng luôn tài khoản cũ**.

---

## 2. Khái niệm cần nắm để giải thích

### 2.1. OAuth2 là gì?

**OAuth2** là chuẩn cho phép một ứng dụng (Cetee) **mượn danh tính** người dùng từ một
nhà cung cấp khác (Google) mà **không bao giờ thấy mật khẩu Google** của họ. Thay vào
đó Google xác nhận "đúng là người này" và trả về thông tin cơ bản (email, tên).

### 2.2. Luồng OAuth2 "Authorization Code" (đơn giản hoá)

```
1. Người dùng bấm "Đăng nhập với Google" trên Cetee.
2. Cetee CHUYỂN HƯỚNG sang trang đăng nhập của Google (kèm ClientId).
3. Người dùng đăng nhập + đồng ý chia sẻ email/profile NGAY TRÊN Google.
4. Google chuyển ngược về Cetee tại địa chỉ callback /signin-google kèm "mã code".
5. Cetee (dùng ClientSecret) đổi "code" lấy thông tin người dùng từ Google.
6. Cetee tìm/tạo user theo email → phát cookie đăng nhập như bình thường.
```

Điểm mấu chốt: **mật khẩu Google không bao giờ đi qua Cetee**. Cetee chỉ nhận email + tên.

### 2.3. ClientId / ClientSecret / Redirect URI

- **ClientId**: định danh công khai của ứng dụng (đăng ký ở Google Cloud Console).
- **ClientSecret**: "mật khẩu" bí mật của ứng dụng — để trong file cấu hình, không đẩy lên git.
- **Redirect URI**: địa chỉ Google được phép chuyển về sau khi xác thực. Dự án dùng
  callback mặc định `/signin-google`.

---

## 3. Luồng hoạt động trong code

```
Trang Login có nút "Đăng nhập với Google"
   → AccountController.ExternalLogin
   → Challenge(props, "Google")             ← chuyển hướng sang Google
   ... người dùng đăng nhập trên Google ...
   → Google gọi về /signin-google (Identity tự xử lý) rồi tới
   → AccountController.ExternalLoginCallback
   → lấy email + name từ thông tin Google trả về
   → AuthService.FindOrCreateExternalUserAsync(email, name)  ← có thì dùng, chưa có thì tạo
   → SignInManager.SignInAsync(user)         ← phát cookie đăng nhập của Cetee
   → vào trang chính
```

---

## 4. Các file liên quan

| File | Vai trò |
|------|---------|
| [Program.cs](../Program.cs) | Đăng ký `.AddGoogle(...)` nếu có cấu hình ClientId/Secret. |
| [Controllers/AccountController.cs](../Controllers/AccountController.cs) | `ExternalLogin` + `ExternalLoginCallback`. |
| [Services/AuthService.cs](../Services/AuthService.cs) | `FindOrCreateExternalUserAsync` — tìm/tạo user theo email Google. |
| [appsettings.json](../appsettings.json) | Khung cấu hình `Authentication:Google` (secret để ở file Development). |

---

## 5. Giải thích code chính

### 5.1. Đăng ký Google trong `Program.cs`

```csharp
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

// Chỉ bật khi đã cấu hình → thiếu cấu hình app vẫn chạy bình thường.
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SignInScheme = IdentityConstants.ExternalScheme; // callback: /signin-google
        options.Scope.Add("email");    // xin quyền đọc email
        options.Scope.Add("profile");  // và thông tin hồ sơ (tên)
    });
}
```

> Cách viết "có cấu hình mới bật" giúp **chạy được ngay cả khi chưa đăng ký Google** —
> nút Google chỉ báo "chưa cấu hình" chứ không làm sập app.

### 5.2. Bước 1 — Chuyển hướng sang Google: `ExternalLogin`

```csharp
public async Task<IActionResult> ExternalLogin(IAuthenticationSchemeProvider schemes,
    string provider = "Google", string? returnUrl = null)
{
    if (await schemes.GetSchemeAsync(provider) is null)   // chưa cấu hình
    {
        TempData["Info"] = "Đăng nhập Google chưa được cấu hình (thiếu Client ID/Secret).";
        return RedirectToAction(nameof(Login));
    }

    var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
    var props = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
    return Challenge(props, provider);   // ← trả về lệnh "chuyển hướng sang Google"
}
```

`Challenge(...)` là lệnh của ASP.NET Core ra hiệu "bắt đầu đăng nhập với provider này".

### 5.3. Bước 2 — Nhận kết quả từ Google: `ExternalLoginCallback`

```csharp
var info = await _signInManager.GetExternalLoginInfoAsync();   // thông tin Google trả về
var email = info.Principal.FindFirstValue(ClaimTypes.Email);
var name  = info.Principal.FindFirstValue(ClaimTypes.Name);

var user = await _auth.FindOrCreateExternalUserAsync(email, name ?? email);
await _signInManager.SignInAsync(user, isPersistent: true);     // phát cookie Cetee
await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme); // dọn cookie tạm của Google
```

### 5.4. Tìm hoặc tạo user — `AuthService.FindOrCreateExternalUserAsync`

```csharp
public async Task<User> FindOrCreateExternalUserAsync(string email, string fullName)
{
    email = email.Trim().ToLowerInvariant();
    var user = await _userManager.FindByEmailAsync(email);
    if (user is not null) return user;          // đã có → dùng luôn

    user = new User
    {
        FullName = string.IsNullOrWhiteSpace(fullName) ? email : fullName.Trim(),
        Email = email, UserName = email,
        EmailConfirmed = true,
        RoleId = userRole.Id,
        AccountType = AccountType.Personal       // đăng nhập Google = tài khoản cá nhân
    };
    await _userManager.CreateAsync(user);        // TẠO KHÔNG CẦN MẬT KHẨU
    return user;
}
```

> Điểm cần nhấn: tài khoản tạo qua Google **không có mật khẩu** (đăng nhập bằng Google).
> Nếu sau này muốn đăng nhập bằng mật khẩu, có thể dùng chức năng **Quên mật khẩu**
> (Chức năng 3) để đặt mật khẩu.

---

## 6. Cách cấu hình (để demo chạy thật)

1. Vào **Google Cloud Console** → tạo **OAuth Client ID** (loại *Web application*).
2. Thêm **Authorized redirect URI**: `http://localhost:5259/signin-google`
   (đúng cổng app đang chạy; khi deploy đổi thành `https://tên-miền/signin-google`).
3. Bỏ ClientId/ClientSecret vào `appsettings.Development.json` (đã `.gitignore`):

```json
{
  "Authentication": {
    "Google": {
      "ClientId": "xxxxx.apps.googleusercontent.com",
      "ClientSecret": "GOCSPX-xxxxx"
    }
  }
}
```

---

## 7. Câu hỏi thường gặp khi bảo vệ (Q&A)

**Q: OAuth2 khác gì đăng nhập thường?**
A: Đăng nhập thường người dùng đưa mật khẩu cho Cetee. OAuth2 thì người dùng đăng nhập
**trên Google**, Google chỉ trả về email + tên; Cetee không thấy mật khẩu Google.

**Q: `ClientSecret` để ở đâu, có an toàn không?**
A: Để trong `appsettings.Development.json` (và `appsettings.Production.json` khi deploy),
đã được `.gitignore` nên **không đẩy lên GitHub**.

**Q: `/signin-google` ở đâu, sao không thấy controller cho nó?**
A: Đó là **endpoint mặc định** do gói `AddGoogle` của Identity tự đăng ký để nhận
callback từ Google. Mình không cần tự viết.

**Q: Tài khoản Google trùng email với tài khoản đã đăng ký thường thì sao?**
A: `FindOrCreateExternalUserAsync` tìm theo email — nếu đã tồn tại thì **dùng lại** tài
khoản cũ, không tạo trùng.

**Q: Vì sao đăng nhập Google lại là loại Personal?**
A: Người đăng nhập Google là người tự do bên ngoài, không thuộc cơ cấu công ty, nên
xếp loại **Cá nhân** (xem Chức năng 4 về phân loại tài khoản).
