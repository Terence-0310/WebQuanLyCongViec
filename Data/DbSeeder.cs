using Microsoft.EntityFrameworkCore;
using Cetee.Models;
using Cetee.Services;

namespace Cetee.Data;

/// <summary>
/// Khởi tạo dữ liệu tối thiểu để ứng dụng vận hành (bootstrap): 2 vai trò
/// Admin/User và một tài khoản Admin để đăng nhập lần đầu. KHÔNG seed dữ liệu
/// mẫu (workspace/project/task...) — toàn bộ dữ liệu nghiệp vụ do người dùng
/// tạo qua giao diện (dữ liệu thực). Chỉ chạy khi các bảng còn trống.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher hasher)
    {
        await db.Database.MigrateAsync();

        // 1. Vai trò bắt buộc (Admin/User). Tạo nếu thiếu.
        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
        if (adminRole is null)
        {
            adminRole = new Role { Name = "Admin" };
            db.Roles.Add(adminRole);
        }

        if (!await db.Roles.AnyAsync(r => r.Name == "Manager"))
            db.Roles.Add(new Role { Name = "Manager" });

        if (!await db.Roles.AnyAsync(r => r.Name == "User"))
            db.Roles.Add(new Role { Name = "User" });

        await db.SaveChangesAsync();

        // 2. Tài khoản Admin bootstrap (chỉ tạo khi chưa có user nào).
        if (!await db.Users.AnyAsync())
        {
            db.Users.Add(new User
            {
                FullName = "Quản trị viên",
                Email = "admin@example.com",
                PasswordHash = hasher.Hash("Admin@123"),
                RoleId = adminRole.Id,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }
}
