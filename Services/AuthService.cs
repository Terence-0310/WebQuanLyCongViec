using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Cetee.Data;
using Cetee.Models;
using Cetee.ViewModels;

namespace Cetee.Services;

public interface IAuthService
{
    Task<(bool Success, string? Error, User? User)> RegisterAsync(RegisterViewModel model);

    /// <summary>Đăng nhập ngoài (Google): tìm user theo email, chưa có thì tạo mới
    /// (vai trò User, tài khoản Cá nhân). Trả về user.</summary>
    Task<User> FindOrCreateExternalUserAsync(string email, string fullName);
}

/// <summary>Nghiệp vụ đăng ký tài khoản và tạo tài khoản đăng nhập ngoài (qua Identity UserManager).</summary>
public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly UserManager<User> _userManager;

    public AuthService(AppDbContext db, UserManager<User> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<(bool Success, string? Error, User? User)> RegisterAsync(RegisterViewModel model)
    {
        string email = model.Email.Trim().ToLowerInvariant();
        if (await _userManager.FindByEmailAsync(email) is not null)
            return (false, "Email đã được sử dụng.", null);

        var userRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == Roles.User);
        if (userRole is null)
            return (false, "Hệ thống chưa khởi tạo vai trò.", null);

        var user = new User
        {
            FullName = model.FullName.Trim(),
            Email = email,
            UserName = email,
            EmailConfirmed = true,
            RoleId = userRole.Id,
            AccountType = AccountType.Personal // Tự đăng ký = tài khoản cá nhân.
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
            return (false, string.Join("; ", result.Errors.Select(e => e.Description)), null);

        return (true, null, user);
    }

    public async Task<User> FindOrCreateExternalUserAsync(string email, string fullName)
    {
        email = email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(email);
        if (user is not null) return user;

        var userRole = await _db.Roles.FirstAsync(r => r.Name == Roles.User);
        user = new User
        {
            FullName = string.IsNullOrWhiteSpace(fullName) ? email : fullName.Trim(),
            Email = email,
            UserName = email,
            EmailConfirmed = true,
            RoleId = userRole.Id,
            AccountType = AccountType.Personal // Đăng nhập Google = tài khoản cá nhân.
        };
        // Tạo không cần mật khẩu (đăng nhập bằng Google).
        await _userManager.CreateAsync(user);
        return user;
    }
}
