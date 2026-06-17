using Microsoft.EntityFrameworkCore;
using Cetee.Data;
using Cetee.Models;
using Cetee.ViewModels;

namespace Cetee.Services;

public interface IUserService
{
    Task<List<UserListItemViewModel>> GetAllAsync(int currentUserId);

    /// <summary>Danh sách người mà người xem được phép xem lịch/thống kê (gồm chính mình).
    /// Admin: tất cả; Manager: bản thân + nhân viên trực thuộc; còn lại: chỉ bản thân.</summary>
    Task<List<User>> GetVisibleEmployeesAsync(int viewerId, string viewerRole);

    /// <summary>Những user có thể làm người quản lý (vai trò Manager hoặc Admin).</summary>
    Task<List<User>> GetManagerCandidatesAsync();

    /// <summary>Xác định phạm vi "xem theo người" từ lựa chọn employeeId trên giao diện.</summary>
    Task<EmployeeScopeResult> ResolveScopeAsync(int viewerId, string viewerRole, int? selectedEmployeeId);

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

        var roleName = NormalizeRole(model.Role);
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
        if (role is null)
            return (false, "Vai trò không hợp lệ.");

        _db.Users.Add(new User
        {
            FullName = model.FullName.Trim(),
            Email = email,
            PasswordHash = _hasher.Hash(model.Password),
            RoleId = role.Id,
            ManagerId = await ResolveManagerIdAsync(model.ManagerId, null),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return (true, null);
    }

    // Chỉ chấp nhận 3 vai trò hợp lệ; mặc định về User nếu lệch.
    private static string NormalizeRole(string? role) =>
        role is "Admin" or "Manager" or "User" ? role : "User";

    // Người quản lý phải tồn tại, có vai trò Manager/Admin, và không phải chính mình.
    private async Task<int?> ResolveManagerIdAsync(int? managerId, int? selfId)
    {
        if (managerId is not int mid || mid == selfId) return null;
        bool valid = await _db.Users.AnyAsync(u => u.Id == mid && (u.Role.Name == "Manager" || u.Role.Name == "Admin"));
        return valid ? mid : null;
    }

    public Task<List<User>> GetVisibleEmployeesAsync(int viewerId, string viewerRole)
    {
        if (viewerRole == "Admin")
            return _db.Users.Include(u => u.Role).OrderBy(u => u.FullName).ToListAsync();

        if (viewerRole == "Manager")
            return _db.Users.Include(u => u.Role)
                .Where(u => u.Id == viewerId || u.ManagerId == viewerId)
                .OrderBy(u => u.FullName)
                .ToListAsync();

        // Nhân viên / người dùng độc lập: chỉ thấy chính mình.
        return _db.Users.Where(u => u.Id == viewerId).ToListAsync();
    }

    public Task<List<User>> GetManagerCandidatesAsync() =>
        _db.Users.Include(u => u.Role)
            .Where(u => u.Role.Name == "Manager" || u.Role.Name == "Admin")
            .OrderBy(u => u.FullName)
            .ToListAsync();

    public async Task<EmployeeScopeResult> ResolveScopeAsync(int viewerId, string viewerRole, int? selectedEmployeeId)
    {
        var visible = await GetVisibleEmployeesAsync(viewerId, viewerRole);
        bool canViewOthers = (viewerRole == "Admin" || viewerRole == "Manager") && visible.Count > 1;

        int selected;
        if (!canViewOthers)
            selected = viewerId;                       // Nhân viên/độc lập: luôn là chính mình.
        else if (selectedEmployeeId is int id && id > 0 && visible.Any(u => u.Id == id))
            selected = id;                             // Chọn một người cụ thể hợp lệ.
        else
            selected = 0;                              // Mặc định / chọn "Tất cả".

        return new EmployeeScopeResult
        {
            Visible = visible,
            SelectedId = selected,
            CanViewOthers = canViewOthers,
            AssigneeFilter = selected == 0 ? null : new List<int> { selected }
        };
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
            ManagerId = user.ManagerId,
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
        var roleName = NormalizeRole(model.Role);
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
        user.ManagerId = await ResolveManagerIdAsync(model.ManagerId, user.Id);

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
                ManagerName = u.Manager != null ? u.Manager.FullName : null,
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

        // Gỡ liên kết nhân viên trực thuộc (nếu user này là quản lý của người khác).
        await _db.Users.Where(u => u.ManagerId == targetUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.ManagerId, (int?)null));

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
