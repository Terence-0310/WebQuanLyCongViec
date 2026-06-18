using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Cetee.Models;

namespace Cetee.Services;

/// <summary>
/// Sinh claim cho cookie đăng nhập của Identity theo quy ước của ứng dụng:
/// Name = họ tên đầy đủ, Role = tên vai trò lấy từ FK trực tiếp <see cref="User.RoleId"/>
/// (hệ thống dùng 1 vai trò/người qua FK, không qua bảng AspNetUserRoles),
/// và một claim "account_type" để phân biệt nhanh tài khoản cá nhân / công ty.
/// NameIdentifier (= User.Id) do Identity tự thêm.
/// </summary>
public class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<User, Role>
{
    private readonly RoleManager<Role> _roleManager;

    public AppUserClaimsPrincipalFactory(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
        _roleManager = roleManager;
    }

    public override async Task<ClaimsPrincipal> CreateAsync(User user)
    {
        var principal = await base.CreateAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;

        // Đổi claim Name (Identity gắn = UserName/email) thành họ tên đầy đủ.
        if (identity.FindFirst(ClaimTypes.Name) is { } nameClaim)
            identity.RemoveClaim(nameClaim);
        identity.AddClaim(new Claim(ClaimTypes.Name, user.FullName));

        // Thêm claim Role theo FK trực tiếp.
        var role = await _roleManager.FindByIdAsync(user.RoleId.ToString());
        if (role?.Name is { } roleName)
            identity.AddClaim(new Claim(ClaimTypes.Role, roleName));

        // Loại tài khoản (Personal/Company) để giao diện điều chỉnh mà không cần truy vấn lại DB.
        identity.AddClaim(new Claim(AppClaims.AccountType, user.AccountType.ToString()));

        return principal;
    }
}
