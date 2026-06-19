using Microsoft.EntityFrameworkCore;
using Cetee.Data;
using Cetee.Models;
using Cetee.ViewModels;

namespace Cetee.Services;

public interface IUserService
{
    Task<List<UserListItemViewModel>> GetAllAsync(int currentUserId);
    Task<(bool Ok, string? Error)> CreateAsync(CreateUserViewModel model);
    Task<EditUserViewModel?> GetForEditAsync(int id, int currentUserId);
    Task<(bool Ok, string? Error)> UpdateAsync(EditUserViewModel model, int currentUserId);
    Task<(bool Ok, string? Error)> SetRoleAsync(int targetUserId, string roleName, int currentUserId);
    Task<(bool Ok, string? Error)> DeleteAsync(int targetUserId, int currentUserId);
}

/// <summary>
/// Nghiệp vụ quản lý người dùng dành cho Admin: xem danh sách, phân quyền
/// Admin/User và xóa tài khoản. Có ràng buộc an toàn: không cho tự đổi quyền/xóa
/// chính mình và không cho hạ quyền/xóa Admin cuối cùng của hệ thống.
/// </summary>
public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;

    public UserService(AppDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(CreateUserViewModel model)
    {
        string email = model.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return (false, "Email đã được sử dụng.");

        // Chỉ chấp nhận hai vai trò hợp lệ; mặc định về User nếu lệch.
        var roleName = model.Role == "Admin" ? "Admin" : "User";
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
        if (role is null)
            return (false, "Vai trò không hợp lệ.");

        _db.Users.Add(new User
        {
            FullName = model.FullName.Trim(),
            Email = email,
            PasswordHash = _hasher.Hash(model.Password),
            RoleId = role.Id,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<EditUserViewModel?> GetForEditAsync(int id, int currentUserId)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return null;

        return new EditUserViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role.Name,
            IsSelf = user.Id == currentUserId
        };
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(EditUserViewModel model, int currentUserId)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == model.Id);
        if (user is null)
            return (false, "Không tìm thấy người dùng.");

        string email = model.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email && u.Id != model.Id))
            return (false, "Email đã được sử dụng.");

        // Đổi vai trò (nếu khác) với các ràng buộc an toàn như khi phân quyền.
        var roleName = model.Role == "Admin" ? "Admin" : "User";
        if (user.Role.Name != roleName)
        {
            if (model.Id == currentUserId)
                return (false, "Bạn không thể tự thay đổi vai trò của chính mình.");
            if (user.Role.Name == "Admin" && roleName != "Admin" && await IsLastAdminAsync(model.Id))
                return (false, "Không thể hạ quyền Admin cuối cùng của hệ thống.");

            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
            if (role is null)
                return (false, "Vai trò không hợp lệ.");
            user.RoleId = role.Id;
        }

        user.FullName = model.FullName.Trim();
        user.Email = email;

        // Chỉ đặt lại mật khẩu khi Admin có nhập mật khẩu mới.
        if (!string.IsNullOrEmpty(model.NewPassword))
            user.PasswordHash = _hasher.Hash(model.NewPassword);

        await _db.SaveChangesAsync();
        return (true, null);
    }

    public Task<List<UserListItemViewModel>> GetAllAsync(int currentUserId) =>
        _db.Users
            .Include(u => u.Role)
            .OrderBy(u => u.Role.Name == "Admin" ? 0 : 1)
            .ThenBy(u => u.FullName)
            .Select(u => new UserListItemViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                RoleName = u.Role.Name,
                CreatedAt = u.CreatedAt,
                OwnedWorkspaceCount = _db.Workspaces.Count(w => w.OwnerId == u.Id),
                AssignedTaskCount = u.AssignedTasks.Count,
                IsSelf = u.Id == currentUserId
            })
            .ToListAsync();

    public async Task<(bool Ok, string? Error)> SetRoleAsync(int targetUserId, string roleName, int currentUserId)
    {
        if (targetUserId == currentUserId)
            return (false, "Bạn không thể tự thay đổi vai trò của chính mình.");

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
        if (role is null)
            return (false, "Vai trò không hợp lệ.");

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId);
        if (user is null)
            return (false, "Không tìm thấy người dùng.");

        if (user.RoleId == role.Id)
            return (true, null); // Không có gì thay đổi.

        // Không cho hạ quyền Admin cuối cùng để tránh mất quyền quản trị hệ thống.
        if (user.Role.Name == "Admin" && roleName != "Admin" && await IsLastAdminAsync(targetUserId))
            return (false, "Không thể hạ quyền Admin cuối cùng của hệ thống.");

        user.RoleId = role.Id;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(int targetUserId, int currentUserId)
    {
        if (targetUserId == currentUserId)
            return (false, "Bạn không thể xóa chính tài khoản đang đăng nhập.");

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId);
        if (user is null)
            return (false, "Không tìm thấy người dùng.");

        if (user.Role.Name == "Admin" && await IsLastAdminAsync(targetUserId))
            return (false, "Không thể xóa Admin cuối cùng của hệ thống.");

        // Gỡ các ràng buộc khóa ngoại kiểu Restrict trước khi xóa user.
        // (Task được giao -> tự SET NULL; Notification/ActivityLog -> tự CASCADE ở DB.)
        await using var tx = await _db.Database.BeginTransactionAsync();

        // Bình luận và tư cách thành viên của user (chặn xóa nếu còn).
        await _db.TaskComments.Where(c => c.UserId == targetUserId).ExecuteDeleteAsync();
        await _db.ProjectMembers.Where(m => m.UserId == targetUserId).ExecuteDeleteAsync();
        await _db.WorkspaceMembers.Where(m => m.UserId == targetUserId).ExecuteDeleteAsync();

        // Workspace do user sở hữu: xóa kèm (DB cascade xuống project/task/page/member).
        await _db.Workspaces.Where(w => w.OwnerId == targetUserId).ExecuteDeleteAsync();

        await _db.Users.Where(u => u.Id == targetUserId).ExecuteDeleteAsync();

        await tx.CommitAsync();
        return (true, null);
    }

    // Còn đúng một Admin và đó chính là user đang xét.
    private async Task<bool> IsLastAdminAsync(int userId)
    {
        var adminIds = await _db.Users
            .Where(u => u.Role.Name == "Admin")
            .Select(u => u.Id)
            .ToListAsync();
        return adminIds.Count <= 1 && adminIds.Contains(userId);
    }
}
