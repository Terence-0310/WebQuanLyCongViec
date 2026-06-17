namespace Cetee.Models;

/// <summary>
/// Mã OTP đặt lại mật khẩu gửi qua email. Mã lưu dưới dạng băm (không lưu plain text),
/// có hạn dùng và giới hạn số lần thử để chống dò mã.
/// </summary>
public class PasswordResetCode
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Mã OTP đã băm (PBKDF2).</summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    /// <summary>Đã dùng để xác minh thành công (không tái sử dụng).</summary>
    public bool Consumed { get; set; }

    /// <summary>Số lần nhập sai (chặn khi vượt ngưỡng).</summary>
    public int Attempts { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
