using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Cetee.Data;
using Cetee.Models;

namespace Cetee.Services;

public interface IPasswordResetService
{
    /// <summary>Tạo và gửi OTP tới email (nếu tồn tại). Luôn "im lặng" để chống dò tài khoản.</summary>
    Task RequestOtpAsync(string email);

    /// <summary>Xác minh OTP. Thành công trả về JWT đặt lại mật khẩu.</summary>
    Task<(bool Ok, string? Error, string? Token)> VerifyOtpAsync(string email, string code);

    /// <summary>Đổi mật khẩu khi JWT đặt lại hợp lệ.</summary>
    Task<(bool Ok, string? Error)> ResetPasswordAsync(string token, string newPassword);
}

/// <summary>
/// Quy trình quên mật khẩu: gửi OTP qua email → xác minh OTP (đổi lấy JWT) → đổi mật khẩu.
/// OTP lưu dạng băm, có hạn 10 phút, giới hạn 5 lần thử; JWT ngắn hạn bảo vệ bước đổi mật khẩu.
/// </summary>
public class PasswordResetService : IPasswordResetService
{
    private const int OtpMinutes = 10;
    private const int MaxAttempts = 5;

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IEmailService _email;
    private readonly IJwtService _jwt;
    private readonly ILogger<PasswordResetService> _log;

    public PasswordResetService(AppDbContext db, IPasswordHasher hasher, IEmailService email,
        IJwtService jwt, ILogger<PasswordResetService> log)
    {
        _db = db;
        _hasher = hasher;
        _email = email;
        _jwt = jwt;
        _log = log;
    }

    public async Task RequestOtpAsync(string email)
    {
        email = email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null)
        {
            _log.LogInformation("Yêu cầu OTP cho email không tồn tại: {Email} (bỏ qua).", email);
            return; // Không tiết lộ email có tồn tại hay không.
        }

        // Hủy các mã chưa dùng trước đó của user này.
        await _db.PasswordResetCodes
            .Where(c => c.UserId == user.Id && !c.Consumed)
            .ExecuteDeleteAsync();

        string otp = GenerateOtp();
        _db.PasswordResetCodes.Add(new PasswordResetCode
        {
            UserId = user.Id,
            CodeHash = _hasher.Hash(otp),
            ExpiresAt = DateTime.UtcNow.AddMinutes(OtpMinutes),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Gửi email OTP. Nếu SMTP lỗi (sai cấu hình/hết hạn app password) thì chỉ ghi log,
        // KHÔNG để request sập — vừa an toàn (im lặng chống dò tài khoản) vừa bền bỉ.
        try
        {
            await _email.SendOtpAsync(user.Email!, user.FullName, otp, OtpMinutes);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Không gửi được email OTP tới {Email} — kiểm tra cấu hình SMTP (Email:User/Password).", user.Email);
        }
    }

    public async Task<(bool Ok, string? Error, string? Token)> VerifyOtpAsync(string email, string code)
    {
        email = email.Trim().ToLowerInvariant();
        code = code.Trim();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null)
            return (false, "Mã không hợp lệ hoặc đã hết hạn.", null);

        var entry = await _db.PasswordResetCodes
            .Where(c => c.UserId == user.Id && !c.Consumed)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (entry is null || entry.ExpiresAt < DateTime.UtcNow)
            return (false, "Mã không hợp lệ hoặc đã hết hạn.", null);
        if (entry.Attempts >= MaxAttempts)
            return (false, "Bạn đã nhập sai quá số lần cho phép. Hãy yêu cầu mã mới.", null);

        if (!_hasher.Verify(code, entry.CodeHash))
        {
            entry.Attempts++;
            await _db.SaveChangesAsync();
            return (false, "Mã OTP không đúng.", null);
        }

        entry.Consumed = true;
        await _db.SaveChangesAsync();

        var token = _jwt.CreateResetToken(user.Id, user.Email!);
        return (true, null, token);
    }

    public async Task<(bool Ok, string? Error)> ResetPasswordAsync(string token, string newPassword)
    {
        var userId = _jwt.ValidateResetToken(token);
        if (userId is null)
            return (false, "Phiên đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng làm lại.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user is null)
            return (false, "Không tìm thấy người dùng.");

        user.PasswordHash = _hasher.Hash(newPassword);
        // Dọn mọi mã OTP còn lại của user.
        await _db.PasswordResetCodes.Where(c => c.UserId == user.Id).ExecuteDeleteAsync();
        await _db.SaveChangesAsync();

        _log.LogInformation("Người dùng {UserId} đã đặt lại mật khẩu thành công.", user.Id);
        return (true, null);
    }

    private static string GenerateOtp() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
