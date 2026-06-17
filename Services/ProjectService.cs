using Microsoft.EntityFrameworkCore;
using Cetee.Data;
using Cetee.Models;
using Cetee.ViewModels;
using TaskStatus = Cetee.Models.TaskStatus;

namespace Cetee.Services;

public interface IProjectService
{
    Task<List<Project>> GetForUserAsync(int userId, bool seeAll);
    Task<Project?> GetByIdForUserAsync(int id, int userId, bool seeAll);
    Task<ProjectDetailsViewModel?> GetDetailsAsync(int id, int userId, string role);
    Task<bool> HasWorkspaceAccessAsync(int workspaceId, int userId, bool seeAll);
    Task<Project?> CreateAsync(ProjectFormViewModel model, int userId, bool seeAll);
    Task<bool> UpdateAsync(ProjectFormViewModel model, int userId, bool seeAll);
    Task<bool> DeleteAsync(int id, int userId, bool seeAll);

    Task<bool> AddMemberAsync(int projectId, int targetUserId, int actingUserId, string actingRole);
    Task<bool> RemoveMemberAsync(int projectId, int targetUserId, int actingUserId, string actingRole);
}

/// <summary>Nghiệp vụ quản lý project, gồm tính tiến độ theo số task hoàn thành.</summary>
public class ProjectService : IProjectService
{
    private readonly AppDbContext _db;
    private readonly IActivityLogService _activity;

    public ProjectService(AppDbContext db, IActivityLogService activity)
    {
        _db = db;
        _activity = activity;
    }

    // Project truy cập được = thuộc workspace mà user là thành viên (SuperAdmin xem tất cả).
    private IQueryable<Project> Accessible(int userId, bool seeAll)
    {
        var query = _db.Projects.AsQueryable();
        return seeAll ? query : query.Where(p => p.Workspace.Members.Any(m => m.UserId == userId));
    }

    public Task<List<Project>> GetForUserAsync(int userId, bool seeAll) =>
        Accessible(userId, seeAll)
            .Include(p => p.Workspace)
            .Include(p => p.Tasks)
            .Include(p => p.Members)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public Task<Project?> GetByIdForUserAsync(int id, int userId, bool seeAll) =>
        Accessible(userId, seeAll)
            .Include(p => p.Workspace)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<ProjectDetailsViewModel?> GetDetailsAsync(int id, int userId, string role)
    {
        bool seeAll = Roles.CanSeeAllData(role);
        var project = await Accessible(userId, seeAll)
            .Include(p => p.Workspace)
            .Include(p => p.Pages)
            .Include(p => p.Tasks).ThenInclude(t => t.Assignee)
            .Include(p => p.Members).ThenInclude(m => m.User).ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project is null) return null;

        // Hoạt động gần đây liên quan tới project và các task của nó.
        var entities = new List<(string, int)> { ("Project", project.Id) };
        entities.AddRange(project.Tasks.Select(t => ("Task", t.Id)));
        var activities = await _activity.GetForEntitiesAsync(entities);

        // Quyền quản lý thành viên: SuperAdmin, chủ workspace, hoặc owner của project.
        bool canManage = CanManageMembers(project, userId, seeAll);
        var addable = new List<User>();
        if (canManage)
        {
            var memberIds = project.Members.Select(m => m.UserId).ToHashSet();
            // Ứng viên = thành viên workspace chưa có trong project (bố trí người từ workspace vào project).
            addable = await _db.WorkspaceMembers
                .Where(m => m.WorkspaceId == project.WorkspaceId && !memberIds.Contains(m.UserId))
                .Select(m => m.User)
                .OrderBy(u => u.FullName)
                .ToListAsync();
        }

        return new ProjectDetailsViewModel
        {
            Project = project,
            Pages = project.Pages.OrderByDescending(p => p.UpdatedAt).ToList(),
            Tasks = project.Tasks.OrderBy(t => t.Status).ThenBy(t => t.DueDate).ToList(),
            Members = project.Members.OrderByDescending(m => m.Role).ToList(),
            RecentActivities = activities,
            AddableUsers = addable,
            CanManageMembers = canManage,
            TotalTasks = project.Tasks.Count,
            CompletedTasks = project.Tasks.Count(t => t.Status == TaskStatus.Done)
        };
    }

    public Task<bool> HasWorkspaceAccessAsync(int workspaceId, int userId, bool seeAll)
    {
        if (seeAll) return _db.Workspaces.AnyAsync(w => w.Id == workspaceId);
        return _db.WorkspaceMembers.AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId);
    }

    public async Task<Project?> CreateAsync(ProjectFormViewModel model, int userId, bool seeAll)
    {
        if (!await HasWorkspaceAccessAsync(model.WorkspaceId, userId, seeAll))
            return null;

        var project = new Project
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            WorkspaceId = model.WorkspaceId
        };
        project.Members.Add(new ProjectMember { UserId = userId, Role = MemberRole.Owner });

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        await _activity.LogAsync(userId, "Created", "Project", project.Id, $"Tạo project \"{project.Name}\"");
        return project;
    }

    public async Task<bool> UpdateAsync(ProjectFormViewModel model, int userId, bool seeAll)
    {
        var project = await GetByIdForUserAsync(model.Id, userId, seeAll);
        if (project is null) return false;

        project.Name = model.Name.Trim();
        project.Description = model.Description?.Trim();
        await _db.SaveChangesAsync();

        await _activity.LogAsync(userId, "Updated", "Project", project.Id, $"Cập nhật project \"{project.Name}\"");
        return true;
    }

    public async Task<bool> DeleteAsync(int id, int userId, bool seeAll)
    {
        var project = await GetByIdForUserAsync(id, userId, seeAll);
        if (project is null) return false;

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
        return true;
    }

    // ---------------------------------------------------------------------
    // Quản lý thành viên project (chủ workspace, owner project, hoặc SuperAdmin).
    // ---------------------------------------------------------------------
    private static bool CanManageMembers(Project project, int userId, bool seeAll) =>
        seeAll
        || project.Workspace.OwnerId == userId
        || project.Members.Any(m => m.UserId == userId && m.Role == MemberRole.Owner);

    public async Task<bool> AddMemberAsync(int projectId, int targetUserId, int actingUserId, string actingRole)
    {
        bool seeAll = Roles.CanSeeAllData(actingRole);
        var project = await Accessible(actingUserId, seeAll)
            .Include(p => p.Workspace)
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null || !CanManageMembers(project, actingUserId, seeAll)) return false;

        // Chỉ bố trí người đã là thành viên của workspace chứa project.
        bool inWorkspace = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == project.WorkspaceId && m.UserId == targetUserId);
        if (!inWorkspace) return false;

        if (project.Members.Any(m => m.UserId == targetUserId)) return true; // đã là thành viên.

        _db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = targetUserId,
            Role = MemberRole.Member
        });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveMemberAsync(int projectId, int targetUserId, int actingUserId, string actingRole)
    {
        bool seeAll = Roles.CanSeeAllData(actingRole);
        var project = await Accessible(actingUserId, seeAll)
            .Include(p => p.Workspace)
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null || !CanManageMembers(project, actingUserId, seeAll)) return false;

        var member = project.Members.FirstOrDefault(m => m.UserId == targetUserId);
        if (member is null) return true;
        if (member.Role == MemberRole.Owner) return false; // Không gỡ owner của project.

        _db.ProjectMembers.Remove(member);
        await _db.SaveChangesAsync();
        return true;
    }
}
