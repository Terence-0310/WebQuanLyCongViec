using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Cetee.Models;

namespace Cetee.Data;

/// <summary>
/// Khởi tạo dữ liệu tối thiểu để ứng dụng vận hành (bootstrap) bằng Identity:
/// 4 vai trò (SuperAdmin/Admin/Manager/User) và đúng MỘT tài khoản SuperAdmin —
/// chủ hệ thống, cấp cao nhất, không thể bị xóa. KHÔNG seed dữ liệu nghiệp vụ.
/// </summary>
public static class DbSeeder
{
    /// <summary>Email tài khoản chủ hệ thống tạo lần đầu.</summary>
    public const string OwnerEmail = "admin@example.com";

    public static async Task SeedAsync(AppDbContext db, RoleManager<Role> roleManager, UserManager<User> userManager)
    {
        await db.Database.MigrateAsync();

        // 1. Bảo đảm đủ 4 vai trò (RoleManager tự đặt NormalizedName).
        foreach (var name in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(name))
                await roleManager.CreateAsync(new Role(name));
        }

        var superAdminRole = await roleManager.FindByNameAsync(Roles.SuperAdmin);
        if (superAdminRole is null) return;

        // 2. Bảo đảm hệ thống luôn có đúng một SuperAdmin.
        if (!await db.Users.AnyAsync(u => u.RoleId == superAdminRole.Id))
        {
            var owner = await db.Users.FirstOrDefaultAsync(u => u.Email == OwnerEmail)
                        ?? await db.Users.OrderBy(u => u.Id).FirstOrDefaultAsync();

            if (owner is null)
            {
                var sa = new User
                {
                    FullName = "Quản trị hệ thống",
                    Email = OwnerEmail,
                    UserName = OwnerEmail,
                    EmailConfirmed = true,
                    RoleId = superAdminRole.Id,
                    AccountType = AccountType.Company,
                    ManagerId = null,
                    CreatedAt = DateTime.UtcNow
                };
                await userManager.CreateAsync(sa, "Admin@123");
            }
            else
            {
                owner.RoleId = superAdminRole.Id;
                owner.AccountType = AccountType.Company;
                owner.ManagerId = null;
                await userManager.UpdateAsync(owner);
            }
        }
    }
}
