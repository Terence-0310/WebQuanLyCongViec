using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Cetee.Models;

namespace Cetee.Services;

public interface IJwtService
{
    /// <summary>Tạo JWT (HS256) cho phép đặt lại mật khẩu — ngắn hạn, ký bằng khóa bí mật.</summary>
    string CreateResetToken(int userId, string email);

    /// <summary>Xác thực token đặt lại; trả về UserId nếu hợp lệ, ngược lại null.</summary>
    int? ValidateResetToken(string token);
}

/// <summary>
/// Phát hành và kiểm tra JWT. Ở dự án này JWT được dùng làm "vé" bảo mật cho luồng
/// đặt lại mật khẩu: sau khi xác minh OTP, server cấp một JWT ngắn hạn; bước đổi mật
/// khẩu chỉ chấp nhận khi token này hợp lệ (stateless, có chữ ký, có hạn).
/// </summary>
public class JwtService : IJwtService
{
    private const string ResetPurpose = "pwd_reset";
    private readonly JwtSettings _s;

    public JwtService(IOptions<JwtSettings> settings) => _s = settings.Value;

    private SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(_s.Key));

    public string CreateResetToken(int userId, string email)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("purpose", ResetPurpose),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _s.Issuer,
            audience: _s.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_s.ResetTokenMinutes),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public int? ValidateResetToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _s.Issuer,
                ValidateAudience = true,
                ValidAudience = _s.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = SigningKey,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            if (principal.FindFirst("purpose")?.Value != ResetPurpose) return null;
            var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return int.TryParse(sub, out var id) ? id : null;
        }
        catch
        {
            return null; // Sai chữ ký, hết hạn, sai issuer/audience...
        }
    }
}
