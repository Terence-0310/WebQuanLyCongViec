using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Cetee.Models;

namespace Cetee.Services;

/// <summary>Băm và xác thực mật khẩu bằng PBKDF2 (không cần thư viện ngoài).</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>
/// PBKDF2 (SHA-256, 100k vòng). Cài cả <see cref="IPasswordHasher{User}"/> của Identity
/// để SignInManager/UserManager dùng đúng định dạng băm này — nhờ vậy mật khẩu cũ
/// (đã lưu dạng "iterations.salt.key") vẫn đăng nhập được sau khi chuyển sang Identity.
/// </summary>
public class PasswordHasher : IPasswordHasher, IPasswordHasher<User>
{
    private const int SaltSize = 16;     // 128-bit salt
    private const int KeySize = 32;      // 256-bit hash
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);
        // Định dạng lưu trữ: iterations.salt.key (base64)
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('.', 3);
        if (parts.Length != 3) return false;

        int iterations = int.Parse(parts[0]);
        byte[] salt = Convert.FromBase64String(parts[1]);
        byte[] key = Convert.FromBase64String(parts[2]);

        byte[] attempt = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, key.Length);
        return CryptographicOperations.FixedTimeEquals(attempt, key);
    }

    // --- Cài đặt IPasswordHasher<User> của Identity, ủy quyền về cùng thuật toán ---
    public string HashPassword(User user, string password) => Hash(password);

    public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword) =>
        Verify(providedPassword, hashedPassword)
            ? PasswordVerificationResult.Success
            : PasswordVerificationResult.Failed;
}
