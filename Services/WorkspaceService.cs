using Microsoft.EntityFrameworkCore;
using Cetee.Data;
using Cetee.Models;
using Cetee.ViewModels;

namespace Cetee.Services;

public interface IWorkspaceService
{
    Task<List<Workspace>> GetForUserAsync(int userId, bool seeAll);
    Task<Workspace?> GetByIdForUserAsync(int id, int userId, bool seeAll);
    Task<WorkspaceDetailsViewModel?> GetDetailsAsync(int id, int userId, string role);
    Task<Workspace> CreateAsync(WorkspaceFormViewModel model, int ownerId, string ownerRole);
    Task<bool> UpdateAsync(WorkspaceFormViewModel model, int userId, bool seeAll);
    Task<bool> DeleteAsync(int id, int userId, bool seeAll);

    Task<bool> AddMemberAsync(int workspaceId, int targetUserId, int actingUserId, string actingRole);
    Task<bool> RemoveMemberAsync(int workspaceId, int targetUserId, int actingUserId, string actingRole);
}

/// <summary>Nghiệp vụ quản lý workspace. User chỉ thấy workspace mình là thành viên; SuperAdmin thấy tất cả.</summary>
public class WorkspaceService : IWorkspaceService
{
    private readonly AppDbContext _db;
    private readonly IUserService _users;

    public WorkspaceService(AppDbContext db, IUserService users)
    {
        _db = db;
        _users = users;
    }

    // Lọc workspace mà người dùng được phép truy cập.
    private IQueryable<Workspace> Accessible(int userId, bool seeAll)
    {
        var query = _db.Workspaces.AsQueryable();
        return seeAll ? query : query.Where(w => w.Members.Any(m => m.UserId == userId));
    }

    public Task<List<Workspace>> GetForUserAsync(int userId, bool seeAll) =>
        Accessible(userId, seeAll)
            .Include(w => w.Owner)
            .Include(w => w.Projects)
            .Include(w => w.Members)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

    public Task<Workspace?> GetByIdForUserAsync(int id, int userId, bool seeAll) =>
        Accessible(userId, seeAll)
            .Include(w => w.Owner)
            .FirstOrDefaultAsync(w => w.Id == id);

    public async Task<WorkspaceDetailsViewModel?> GetDetailsAsync(int id, int userId, string role)
    {
        bool seeAll = Roles.CanSeeAllData(role);
        var ws = await Accessible(userId, seeAll)
            .Include(w => w.Owner)
            .Include(w => w.Members).ThenInclude(m => m.User).ThenInclude(u => u.Role)
            .Include(w => w.Projects).ThenInclude(p => p.Members)
            .Include(w => w.Projects).ThenInclude(p => p.Tasks)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (ws is null) return null;

        bool canManage = seeAll || ws.OwnerId == userId;
        var addable = new List<User>();
        if (canManage)
        {
            var memberIds = ws.Members.Select(m => m.UserId).ToHashSet();
            addable = (await _users.GetVisibleEmployeesAsync(userId, role))
                .Where(u => !memberIds.Contains(u.Id))
                .ToList();
        }

        return new WorkspaceDetailsViewModel
        {
            Workspace = ws,
            Members = ws.Members.OrderByDescending(m => m.Role).ThenBy(m => m.User.FullName).ToList(),
            Projects = ws.Projects.OrderByDescending(p => p.CreatedAt).ToList(),
            AddableUsers = addable,
            CanManageMembers = canManage
        };
    }

    public async Task<Workspace> CreateAsync(WorkspaceFormViewModel model, int ownerId, string ownerRole)
    {
        var workspace = new Workspace
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            OwnerId = ownerId
        };
        // Người tạo tự động trở thành thành viên với vai trò Owner.
        workspace.Members.Add(new WorkspaceMember { UserId = ownerId, Role = MemberRole.Owner });

        // Thêm các thành viên được chọn — chỉ chấp nhận người trong phạm vi quản lý của người tạo.
        if (model.MemberIds.Count > 0)
        {
            var allowed = (await _users.GetVisibleEmployeesAsync(ownerId, ownerRole))
                .Select(u => u.Id).ToHashSet();
            foreach (var uid in model.MemberIds.Distinct())
            {
                if (uid != ownerId && allowed.Contains(uid))
                    workspace.Members.Add(new WorkspaceMember { UserId = uid, Role = MemberRole.Member });
            }
        }

        _db.Workspaces.Add(workspace);
        await _db.SaveChangesAsync();
        return workspace;
    }

    public async Task<bool> UpdateAsync(WorkspaceFormViewModel model, int userId, bool seeAll)
    {
        var workspace = await GetByIdForUserAsync(model.Id, userId, seeAll);
        if (workspace is null) return false;

        workspace.Name = model.Name.Trim();
        workspace.Description = model.Description?.Trim();
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id, int userId, bool seeAll)
    {
        var workspace = await GetByIdForUserAsync(id, userId, seeAll);
        if (workspace is null) return false;

        _db.Workspaces.Remove(workspace);
        await _db.SaveChangesAsync();
        return true;
    }

    // ---------------------------------------------------------------------
    // Quản lý thành viên (chỉ chủ sở hữu workspace hoặc SuperAdmin).
    // ---------------------------------------------------------------------
    public async Task<bool> AddMemberAsync(int workspaceId, int targetUserId, int actingUserId, string actingRole)
    {
        bool seeAll = Roles.CanSeeAllData(actingRole);
        var ws = await Accessible(actingUserId, seeAll)
            .Include(w => w.Members)
            .FirstOrDefaultAsync(w => w.Id == workspaceId);
        if (ws is null || !(seeAll || ws.OwnerId == actingUserId)) return false;

        // Chỉ thêm người trong phạm vi quản lý của người thao tác (đội của họ).
        var allowed = (await _users.GetVisibleEmployeesAsync(actingUserId, actingRole))
            .Select(u => u.Id).ToHashSet();
        if (!allowed.Contains(targetUserId)) return false;

        if (ws.Members.Any(m => m.UserId == targetUserId)) return true; // đã là thành viên.

        _db.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = workspaceId,
            UserId = targetUserId,
            Role = MemberRole.Member
        });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveMemberAsync(int workspaceId, int targetUserId, int actingUserId, string actingRole)
    {
        bool seeAll = Roles.CanSeeAllData(actingRole);
        var ws = await Accessible(actingUserId, seeAll)
            .FirstOrDefaultAsync(w => w.Id == workspaceId);
        if (ws is null || !(seeAll || ws.OwnerId == actingUserId)) return false;

        // Không cho gỡ chủ sở hữu khỏi chính workspace của họ.
        if (targetUserId == ws.OwnerId) return false;

        // Gỡ khỏi workspace và đồng thời khỏi các project thuộc workspace đó.
        await _db.ProjectMembers
            .Where(pm => pm.UserId == targetUserId && pm.Project.WorkspaceId == workspaceId)
            .ExecuteDeleteAsync();
        await _db.WorkspaceMembers
            .Where(m => m.WorkspaceId == workspaceId && m.UserId == targetUserId)
            .ExecuteDeleteAsync();
        return true;
    }
}
