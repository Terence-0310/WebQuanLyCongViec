namespace Cetee.Models;

/// <summary>Cấu hình JWT. Bind từ section "Jwt" trong appsettings.</summary>
public class JwtSettings
{
    public string Issuer { get; set; } = "Cetee";
    public string Audience { get; set; } = "CeteeUsers";

    /// <summary>Khóa bí mật ký HS256 (để trong appsettings.Development.json, không commit).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Thời hạn token đặt lại mật khẩu (phút).</summary>
    public int ResetTokenMinutes { get; set; } = 15;
}
