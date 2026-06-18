using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Cetee.Data;
using Cetee.Models;
using Cetee.ViewModels;

namespace Cetee.Services;

public interface IUserService
{
    /// <summary>Trang quản lý người dùng trong phạm vi quản lý của người xem.</summary>
    Task<UserIndexViewModel> GetIndexAsync(int viewerId, string viewerRole);

    /// <summary>Danh sách người mà người xem được phép xem (gồm chính mình) — dùng cho
    /// bộ chọn "Xem theo người". SuperAdmin: tất cả; Admin: đội của mình (manager +
    /// user thuộc các manager đó); Manager: bản thân + user trực thuộc; còn lại: chỉ mình.</summary>
    Task<List<User>> GetVisibleEmployeesAsync(int viewerId, string viewerRole);

    /// <summary>Xác định phạm vi "xem theo người" từ lựa chọn employeeId trên giao diện.</summary>
    Task<EmployeeScopeResult> ResolveScopeAsync(int viewerId, string viewerRole, int? selectedEmployeeId);

    Task<(bool Ok, string? Error)> CreateAsync(CreateUserViewModel model, int creatorId, string creatorRole);
    Task<EditUserViewModel?> GetForEditAsync(int id, int viewerId, string viewerRole);
    Task<(bool Ok, string? Error)> UpdateAsync(EditUserViewModel model, int viewerId, string viewerRole);
    Task<(bool Ok, string? Error)> SetRoleAsync(int targetUserId, string roleName, int viewerId, string viewerRole);
    Task<(bool Ok, string? Error)> DeleteAsync(int targetUserId, int viewerId, string viewerRole);
}

/// <summary>
/// Nghiệp vụ quản lý người dùng theo phân cấp công ty:
/// SuperAdmin → Admin → Manager → User (xem <see cref="Roles"/>).
///
/// Nguyên tắc:
///  - Một người chỉ quản lý (xem/sửa/xóa) những người có cấp thấp hơn và nằm trong
///    "đội" của mình — tức trực thuộc trực tiếp hoặc gián tiếp qua một cấp trung gian
///    (sâu tối đa 2 cấp: Admin thấy Manager và các User của Manager đó).
///  - SuperAdmin thấy và quản lý toàn bộ; SuperAdmin không bao giờ bị xóa và chỉ
///    SuperAdmin mới biết tổng số Admin / cấp-bỏ quyền Admin.
///  - Người tạo trở thành cấp trên trực tiếp của người được tạo (ManagerId = người tạo).
///  - User tự đăng ký là "User độc lập" (không trực thuộc ai) để tự quản lý việc cá nhân.
/// </summary>
public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly UserManager<User> _userManager;

    public UserService(AppDbContext db, IPasswordHasher hasher, UserManager<User> userManager)
    {
        _db = db;
        _hasher = hasher;
        _userManager = userManager;
    }

    // ---------------------------------------------------------------------
    // Phạm vi nhìn thấy (đội của người xem), sâu tối đa 2 cấp + chính mình.
    // ---------------------------------------------------------------------
    private IQueryable<User> VisibleUsers(int viewerId, string viewerRole)
    {
        var query = _db.Users.Include(u => u.Role).Include(u => u.Manager);
        if (Roles.CanSeeAllData(viewerRole))
            return query;

        return query.Where(u =>
            u.Id == viewerId
            || u.ManagerId == viewerId
            || (u.Manager != null && u.Manager.ManagerId == viewerId));
    }

    public async Task<List<User>> GetVisibleEmployeesAsync(int viewerId, string viewerRole) =>
        // "Nhân viên" = tài khoản công ty; loại trừ tài khoản cá nhân khỏi bộ chọn người
        // và khỏi danh sách ứng viên thêm vào workspace/project.
        await VisibleUsers(viewerId, viewerRole)
            .Where(u => u.AccountType == AccountType.Company)
            .OrderBy(u => u.FullName)
            .ToListAsync();

    public async Task<EmployeeScopeResult> ResolveScopeAsync(int viewerId, string viewerRole, int? selectedEmployeeId)
    {
        var visible = await GetVisibleEmployeesAsync(viewerId, viewerRole);

        // Chỉ những vai trò quản lý (Manager trở lên) và thực sự có người khác để xem
        // mới hiển thị bộ chọn.
        bool canViewOthers = Roles.Level(viewerRole) >= Roles.Level(Roles.Manager) && visible.Count > 1;

        int selected;
        if (!canViewOthers)
            selected = viewerId;                                                  // Luôn là chính mình.
        else if (selectedEmployeeId is int id && id > 0 && visible.Any(u => u.Id == id))
            selected = id;                                                        // Một người cụ thể hợp lệ.
        else
            selected = 0;                                                         // "Tất cả".

        return new EmployeeScopeResult
        {
            Visible = visible,
            SelectedId = selected,
            CanViewOthers = canViewOthers,
            AssigneeFilter = selected == 0 ? null : new List<int> { selected }
        };
    }

    // ---------------------------------------------------------------------
    // Quyền quản lý một người cụ thể.
    // ---------------------------------------------------------------------
    /// <summary>Người xem có quyền quản lý (sửa/đổi vai trò/xóa) <paramref name="target"/> không.
    /// Yêu cầu: target có cấp THẤP HƠN người xem và nằm trong đội của người xem
    /// (SuperAdmin quản lý mọi cấp dưới). Không bao giờ quản lý được chính mình.</summary>
    private static bool CanManage(int viewerId, string viewerRole, User target)
    {
        if (target.Id == viewerId) return false;
        if (Roles.Level(target.Role.Name) >= Roles.Level(viewerRole)) return false;
        if (Roles.CanSeeAllData(viewerRole)) return true; // SuperAdmin: toàn quyền cấp dưới.

        // Trong đội: trực thuộc trực tiếp hoặc qua một cấp trung gian.
        return target.ManagerId == viewerId
            || (target.Manager != null && target.Manager.ManagerId == viewerId);
    }

    // ---------------------------------------------------------------------
    // Danh sách quản lý người dùng.
    // ---------------------------------------------------------------------
    public async Task<UserIndexViewModel> GetIndexAsync(int viewerId, string viewerRole)
    {
        var users = await VisibleUsers(viewerId, viewerRole).ToListAsync();

        var rows = users
            .OrderByDescending(u => Roles.Level(u.Role.Name)) // cấp cao lên trước
            .ThenBy(u => u.FullName)
            .Select(u => new UserListItemViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email!,
                RoleName = u.Role.Name!,
                AccountType = u.AccountType,
                ManagerName = u.Manager?.FullName,
                CreatedAt = u.CreatedAt,
                OwnedWorkspaceCount = _db.Workspaces.Count(w => w.OwnerId == u.Id),
                AssignedTaskCount = _db.TaskAssignees.Count(a => a.UserId == u.Id),
                IsSelf = u.Id == viewerId,
                CanEdit = CanManage(viewerId, viewerRole, u) || u.Id == viewerId,
                CanDelete = CanManage(viewerId, viewerRole, u),
                CanToggleAdmin = viewerRole == Roles.SuperAdmin && u.Id != viewerId && u.Role.Name != Roles.SuperAdmin
            })
            .ToList();

        return new UserIndexViewModel
        {
            Users = rows,
            ViewerRole = viewerRole,
            CanCreate = Roles.AssignableBy(viewerRole).Any(),
            // Chỉ SuperAdmin được biết tổng số Admin của hệ thống.
            TotalAdmins = viewerRole == Roles.SuperAdmin
                ? await _db.Users.CountAsync(u => u.Role.Name == Roles.Admin)
                : null
        };
    }

    // ---------------------------------------------------------------------
    // Tạo người dùng.
    // ---------------------------------------------------------------------
    public async Task<(bool Ok, string? Error)> CreateAsync(CreateUserViewModel model, int creatorId, string creatorRole)
    {
        if (!Roles.AssignableBy(creatorRole).Contains(model.Role))
            return (false, "Bạn không được phép tạo người dùng với vai trò này.");

        string email = model.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return (false, "Email đã được sử dụng.");

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == model.Role);
        if (role is null)
            return (false, "Vai trò không hợp lệ.");

        var user = new User
        {
            FullName = model.FullName.Trim(),
            Email = email,
            UserName = email,
            EmailConfirmed = true,
            RoleId = role.Id,
            ManagerId = creatorId, // Người tạo là cấp trên trực tiếp.
            AccountType = AccountType.Company, // Do cấp quản lý tạo = nhân viên công ty.
            CreatedAt = DateTime.UtcNow
        };
        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
            return (false, string.Join("; ", result.Errors.Select(e => e.Description)));

        return (true, null);
    }

    // ---------------------------------------------------------------------
    // Sửa người dùng.
    // ---------------------------------------------------------------------
    public async Task<EditUserViewModel?> GetForEditAsync(int id, int viewerId, string viewerRole)
    {
        var user = await _db.Users.Include(u => u.Role).Include(u => u.Manager)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return null;

        bool isSelf = user.Id == viewerId;
        if (!isSelf && !CanManage(viewerId, viewerRole, user)) return null;

        return new EditUserViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            Role = user.Role.Name!,
            IsSelf = isSelf,
            RoleOptions = RoleOptionsFor(viewerRole, user.Role.Name)
        };
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(EditUserViewModel model, int viewerId, string viewerRole)
    {
        var user = await _db.Users.Include(u => u.Role).Include(u => u.Manager)
            .FirstOrDefaultAsync(u => u.Id == model.Id);
        if (user is null)
            return (false, "Không tìm thấy người dùng.");

        bool isSelf = user.Id == viewerId;
        if (!isSelf && !CanManage(viewerId, viewerRole, user))
            return (false, "Bạn không có quyền sửa người dùng này.");

        string email = model.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email && u.Id != model.Id))
            return (false, "Email đã được sử dụng.");

        // Đổi vai trò: không áp dụng cho chính mình; vai trò mới phải nằm trong quyền gán.
        if (!isSelf && user.Role.Name != model.Role)
        {
            if (!Roles.AssignableBy(viewerRole).Contains(model.Role))
                return (false, "Bạn không được phép gán vai trò này.");

            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == model.Role);
            if (role is null)
                return (false, "Vai trò không hợp lệ.");
            user.RoleId = role.Id;
        }

        user.FullName = model.FullName.Trim();
        user.Email = email;
        // Đồng bộ các trường chuẩn hóa của Identity để đăng nhập theo email vẫn đúng.
        user.NormalizedEmail = email.ToUpperInvariant();
        user.UserName = email;
        user.NormalizedUserName = email.ToUpperInvariant();

        if (!string.IsNullOrEmpty(model.NewPassword))
            user.PasswordHash = _hasher.Hash(model.NewPassword);

        await _db.SaveChangesAsync();
        return (true, null);
    }

    // ---------------------------------------------------------------------
    // Cấp / bỏ quyền Admin (chỉ SuperAdmin).
    // ---------------------------------------------------------------------
    public async Task<(bool Ok, string? Error)> SetRoleAsync(int targetUserId, string roleName, int viewerId, string viewerRole)
    {
        if (viewerRole != Roles.SuperAdmin)
            return (false, "Chỉ Super Admin được cấp hoặc bỏ quyền Admin.");
        if (targetUserId == viewerId)
            return (false, "Bạn không thể tự thay đổi vai trò của chính mình.");
        if (!Roles.AssignableBy(viewerRole).Contains(roleName))
            return (false, "Vai trò không hợp lệ.");

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId);
        if (user is null)
            return (false, "Không tìm thấy người dùng.");
        if (user.Role.Name == Roles.SuperAdmin)
            return (false, "Không thể đổi vai trò của Super Admin.");

        if (user.Role.Name == roleName)
            return (true, null); // Không có gì thay đổi.

        var role = await _db.Roles.FirstAsync(r => r.Name == roleName);
        user.RoleId = role.Id;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    // ---------------------------------------------------------------------
    // Xóa người dùng.
    // ---------------------------------------------------------------------
    public async Task<(bool Ok, string? Error)> DeleteAsync(int targetUserId, int viewerId, string viewerRole)
    {
        var user = await _db.Users.Include(u => u.Role).Include(u => u.Manager)
            .FirstOrDefaultAsync(u => u.Id == targetUserId);
        if (user is null)
            return (false, "Không tìm thấy người dùng.");

        if (user.Role.Name == Roles.SuperAdmin)
            return (false, "Không thể xóa Super Admin dưới mọi hình thức.");
        if (!CanManage(viewerId, viewerRole, user))
            return (false, "Bạn không có quyền xóa người dùng này.");

        // Gỡ các ràng buộc khóa ngoại kiểu Restrict trước khi xóa user.
        // (Notification/ActivityLog -> tự CASCADE ở DB.)
        await using var tx = await _db.Database.BeginTransactionAsync();

        // Gỡ liên kết nhân viên trực thuộc (nếu user này là cấp trên của người khác).
        await _db.Users.Where(u => u.ManagerId == targetUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.ManagerId, (int?)null));

        // Phân công task, bình luận và tư cách thành viên của user.
        await _db.TaskAssignees.Where(a => a.UserId == targetUserId).ExecuteDeleteAsync();
        await _db.TaskComments.Where(c => c.UserId == targetUserId).ExecuteDeleteAsync();
        await _db.ProjectMembers.Where(m => m.UserId == targetUserId).ExecuteDeleteAsync();
        await _db.WorkspaceMembers.Where(m => m.UserId == targetUserId).ExecuteDeleteAsync();

        // Workspace do user sở hữu: xóa kèm (DB cascade xuống project/task/page/member).
        await _db.Workspaces.Where(w => w.OwnerId == targetUserId).ExecuteDeleteAsync();

        await _db.Users.Where(u => u.Id == targetUserId).ExecuteDeleteAsync();

        await tx.CommitAsync();
        return (true, null);
    }

    // Các vai trò người xem được phép gán, dạng option cho dropdown (đánh dấu vai trò hiện tại).
    private static List<SelectListItem> RoleOptionsFor(string viewerRole, string? currentRole)
    {
        var assignable = Roles.AssignableBy(viewerRole).ToList();

        // Khi sửa, vai trò hiện tại của người được sửa có thể bằng cấp người xem (không nằm
        // trong danh sách gán được) — vẫn hiển thị để không mất thông tin, nhưng đã khóa ở View.
        if (currentRole != null && !assignable.Contains(currentRole))
            assignable.Insert(0, currentRole);

        return assignable
            .Select(r => new SelectListItem(Roles.Label(r), r, r == currentRole))
            .ToList();
    }
}
