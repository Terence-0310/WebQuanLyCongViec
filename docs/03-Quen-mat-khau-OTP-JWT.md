# Chức năng 3 — Quên mật khẩu bằng OTP Gmail & JWT

## 1. Mục tiêu

Người dùng quên mật khẩu có thể tự đặt lại qua 3 bước:

```
Nhập email  →  Nhận mã OTP 6 số qua Gmail  →  Nhập OTP  →  Đặt mật khẩu mới
```

Quy trình được bảo mật bằng: OTP **băm trong DB**, **hết hạn 10 phút**, **tối đa 5 lần
thử**; và một **JWT ngắn hạn** làm "vé" bảo vệ riêng bước đổi mật khẩu.

---

## 2. Khái niệm cần nắm để giải thích

### 2.1. OTP là gì?

**OTP (One-Time Password)** = mã dùng một lần, ở đây là **6 chữ số** gửi qua email.
Người dùng phải nhập đúng mã này để chứng minh "tôi đang kiểm soát email đó".

### 2.2. JWT là gì? Vì sao cần thêm JWT?

**JWT (JSON Web Token)** là một chuỗi token **tự chứa thông tin + có chữ ký**. Server
ký bằng khóa bí mật; sau đó chỉ cần kiểm tra chữ ký là biết token thật/giả, **không cần
lưu gì trong database** (stateless).

Trong dự án, JWT dùng làm **"vé đặt lại mật khẩu"**:

```
Bước OTP đúng  →  server cấp 1 JWT ngắn hạn (vài phút)
Bước đổi mật khẩu  →  chỉ chấp nhận khi cầm đúng vé JWT này
```

> Ý nghĩa: tách rời "đã xác minh OTP" (vé) khỏi "đổi mật khẩu". Người dùng không thể
> nhảy thẳng vào trang đổi mật khẩu nếu không có vé hợp lệ, kể cả khi biết URL.

### 2.3. Vì sao OTP phải băm, có hạn, giới hạn lần thử?

- **Băm**: nếu lộ database, kẻ xấu cũng không đọc được mã gốc.
- **Hết hạn 10 phút**: giới hạn thời gian tấn công.
- **Tối đa 5 lần thử**: chống dò mã bằng cách thử liên tục (brute force).
- **Im lặng khi email không tồn tại**: không tiết lộ email nào có trong hệ thống.

---

## 3. Luồng hoạt động

```
[Bước 1] ForgotPassword (nhập email)
   → PasswordResetService.RequestOtpAsync
       • tìm user theo email (không có → im lặng kết thúc)
       • huỷ các OTP cũ chưa dùng
       • sinh OTP 6 số → BĂM → lưu DB (hạn 10', attempts=0)
       • gửi OTP qua Gmail (EmailService)

[Bước 2] VerifyOtp (nhập mã)
   → PasswordResetService.VerifyOtpAsync
       • lấy OTP mới nhất chưa dùng
       • kiểm tra: còn hạn? chưa quá 5 lần? mã đúng?
       • đúng → đánh dấu Consumed = true, CẤP JWT (vé)
       • sai → attempts++ , báo lỗi

[Bước 3] ResetPassword (nhập mật khẩu mới)
   → PasswordResetService.ResetPasswordAsync
       • xác thực JWT (đúng chữ ký? còn hạn? đúng "purpose"?)
       • hợp lệ → băm mật khẩu mới, lưu, xoá hết OTP còn lại
```

---

## 4. Các file liên quan

| File | Vai trò |
|------|---------|
| [Controllers/AccountController.cs](../Controllers/AccountController.cs) | 3 cặp action: ForgotPassword / VerifyOtp / ResetPassword. |
| [Services/PasswordResetService.cs](../Services/PasswordResetService.cs) | Toàn bộ nghiệp vụ 3 bước. |
| [Services/JwtService.cs](../Services/JwtService.cs) | Tạo & xác thực JWT "vé" đặt lại mật khẩu. |
| [Services/EmailService.cs](../Services/EmailService.cs) | Gửi email OTP qua Gmail SMTP (MailKit). |
| [Models/PasswordResetCode.cs](../Models/PasswordResetCode.cs) | Entity lưu OTP đã băm + hạn + số lần thử. |
| [Models/JwtSettings.cs](../Models/JwtSettings.cs) / [Models/EmailSettings.cs](../Models/EmailSettings.cs) | Cấu hình JWT & Email. |

---

## 5. Giải thích code chính

### 5.1. Bước 1 — Sinh & gửi OTP (`RequestOtpAsync`)

```csharp
var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
if (user is null)
{
    _log.LogInformation("...email không tồn tại...(bỏ qua).");
    return;                       // ← IM LẶNG: không tiết lộ email có tồn tại không
}

await _db.PasswordResetCodes.Where(c => c.UserId == user.Id && !c.Consumed)
    .ExecuteDeleteAsync();        // huỷ OTP cũ chưa dùng

string otp = GenerateOtp();       // 6 số ngẫu nhiên
_db.PasswordResetCodes.Add(new PasswordResetCode
{
    UserId = user.Id,
    CodeHash = _hasher.Hash(otp),               // ← LƯU BĂM, không lưu mã gốc
    ExpiresAt = DateTime.UtcNow.AddMinutes(10), // hạn 10 phút
});
await _db.SaveChangesAsync();

try { await _email.SendOtpAsync(user.Email!, user.FullName, otp, 10); }
catch (Exception ex) { _log.LogError(ex, "Không gửi được email OTP..."); } // lỗi SMTP không làm sập request
```

OTP sinh bằng bộ ngẫu nhiên **an toàn mã hoá**:

```csharp
private static string GenerateOtp() =>
    RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6"); // 000000–999999
```

### 5.2. Bước 2 — Xác minh OTP, cấp JWT (`VerifyOtpAsync`)

```csharp
var entry = await _db.PasswordResetCodes
    .Where(c => c.UserId == user.Id && !c.Consumed)
    .OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync();

if (entry is null || entry.ExpiresAt < DateTime.UtcNow)   // hết hạn
    return (false, "Mã không hợp lệ hoặc đã hết hạn.", null);
if (entry.Attempts >= 5)                                  // quá số lần
    return (false, "Bạn đã nhập sai quá số lần cho phép...", null);

if (!_hasher.Verify(code, entry.CodeHash))                // SAI
{
    entry.Attempts++;
    await _db.SaveChangesAsync();
    return (false, "Mã OTP không đúng.", null);
}

entry.Consumed = true;                                    // ĐÚNG → dùng 1 lần
await _db.SaveChangesAsync();
var token = _jwt.CreateResetToken(user.Id, user.Email!);  // ← cấp JWT (vé)
return (true, null, token);
```

### 5.3. Tạo JWT — `JwtService.CreateResetToken`

```csharp
var claims = new[]
{
    new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()), // user là ai
    new Claim("purpose", "pwd_reset"),                          // vé này CHỈ để đổi mật khẩu
    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
};
var token = new JwtSecurityToken(
    issuer: _s.Issuer, audience: _s.Audience, claims: claims,
    expires: DateTime.UtcNow.AddMinutes(_s.ResetTokenMinutes),  // ngắn hạn
    signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)); // ký HS256
return new JwtSecurityTokenHandler().WriteToken(token);
```

### 5.4. Bước 3 — Xác thực JWT & đổi mật khẩu

`JwtService.ValidateResetToken` kiểm tra chữ ký, issuer, audience, hạn, và đúng `purpose`:

```csharp
var principal = handler.ValidateToken(token, new TokenValidationParameters
{
    ValidateIssuer = true, ValidIssuer = _s.Issuer,
    ValidateAudience = true, ValidAudience = _s.Audience,
    ValidateIssuerSigningKey = true, IssuerSigningKey = SigningKey,
    ValidateLifetime = true,                  // còn hạn không
}, out _);
if (principal.FindFirst("purpose")?.Value != "pwd_reset") return null; // đúng mục đích
```

Rồi `ResetPasswordAsync` đổi mật khẩu khi vé hợp lệ:

```csharp
var userId = _jwt.ValidateResetToken(token);
if (userId is null) return (false, "Phiên đặt lại mật khẩu không hợp lệ hoặc đã hết hạn...");

user.PasswordHash = _hasher.Hash(newPassword);                 // băm mật khẩu mới
await _db.PasswordResetCodes.Where(c => c.UserId == user.Id).ExecuteDeleteAsync(); // dọn OTP
await _db.SaveChangesAsync();
```

### 5.5. Gửi email — `EmailService` (MailKit / Gmail SMTP)

```csharp
using var client = new SmtpClient();
var socket = _s.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
await client.ConnectAsync(_s.Host, _s.Port, socket);          // smtp.gmail.com:465
await client.AuthenticateAsync(_s.User, _s.Password.Replace(" ", "")); // App Password
await client.SendAsync(msg);
```

> Cần **Gmail App Password** (bật 2FA → tạo mật khẩu ứng dụng 16 ký tự). Đây là mật
> khẩu *chỉ để gửi mail*, khác hoàn toàn đăng nhập Google.

---

## 6. Câu hỏi thường gặp khi bảo vệ (Q&A)

**Q: OTP và JWT khác nhau thế nào, sao dùng cả hai?**
A: OTP chứng minh "người dùng kiểm soát email". JWT là **vé tạm** cấp sau khi OTP đúng,
để bảo vệ bước đổi mật khẩu — tách 2 bước, không cho nhảy cóc.

**Q: JWT có lưu trong database không?**
A: Không. JWT **stateless** — tự chứa thông tin + chữ ký, server chỉ kiểm tra chữ ký.
Còn OTP thì có lưu (băm) để kiểm tra hạn và số lần thử.

**Q: Lỡ ai đó nhập email người khác để phá thì sao?**
A: Hệ thống **im lặng** nếu email không tồn tại (không lộ thông tin) và OTP chỉ gửi tới
chính email đó — kẻ phá không đọc được mã.

**Q: Vì sao OTP băm chứ không lưu thẳng?**
A: Nếu database bị lộ, mã băm không đọc ngược ra mã gốc → an toàn hơn. So khớp bằng cách
băm lại mã người dùng nhập rồi so sánh.

**Q: `purpose = "pwd_reset"` để làm gì?**
A: Đánh dấu vé này **chỉ dùng để đổi mật khẩu**. Nếu sau này có loại JWT khác, không thể
lấy nhầm vé này dùng sai mục đích.

**Q: Gửi mail lỗi thì luồng có sập không?**
A: Không. Lỗi SMTP chỉ ghi log, request vẫn trả về bình thường (vừa an toàn vừa bền bỉ).
