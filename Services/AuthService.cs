using Microsoft.EntityFrameworkCore;
using Cetee.Data;
using Cetee.Models;
using Cetee.ViewModels;

namespace Cetee.Services;

public interface IAuthService
{
    Task<(bool Success, string? Error, User? User)> RegisterAsync(RegisterViewModel model);
    Task<User?> ValidateCredentialsAsync(string email, string password);
}

/// <summary>Xử lý nghiệp vụ đăng ký và xác thực đăng nhập.</summary>
public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;

    public AuthService(AppDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<(bool Success, string? Error, User? User)> RegisterAsync(RegisterViewModel model)
    {
        string email = model.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return (false, "Email đã được sử dụng.", null);

        // Người dùng đăng ký mới luôn nhận vai trò "User".
        var userRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "User")
                       ?? new Role { Name = "User" };

        var user = new User
        {
            FullName = model.FullName.Trim(),
            Email = email,
            PasswordHash = _hasher.Hash(model.Password),
            Role = userRole
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return (true, null, user);
    }

    public async Task<User?> ValidateCredentialsAsync(string email, string password)
    {
        email = email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || !_hasher.Verify(password, user.PasswordHash))
            return null;

        return user;
    }
}
