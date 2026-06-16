using Microsoft.EntityFrameworkCore;
using Cetee.Data;
using Cetee.Models;
using Cetee.ViewModels;
using TaskStatus = Cetee.Models.TaskStatus;

namespace Cetee.Services;

public interface IProjectService
{
    Task<List<Project>> GetForUserAsync(int userId, bool isAdmin);
    Task<Project?> GetByIdForUserAsync(int id, int userId, bool isAdmin);
    Task<ProjectDetailsViewModel?> GetDetailsAsync(int id, int userId, bool isAdmin);
    Task<bool> HasWorkspaceAccessAsync(int workspaceId, int userId, bool isAdmin);
    Task<Project?> CreateAsync(ProjectFormViewModel model, int userId, bool isAdmin);
    Task<bool> UpdateAsync(ProjectFormViewModel model, int userId, bool isAdmin);
    Task<bool> DeleteAsync(int id, int userId, bool isAdmin);
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

    // Project truy cập được = thuộc workspace mà user là thành viên (hoặc Admin xem tất cả).
    private IQueryable<Project> Accessible(int userId, bool isAdmin)
    {
        var query = _db.Projects.AsQueryable();
        return isAdmin ? query : query.Where(p => p.Workspace.Members.Any(m => m.UserId == userId));
    }

    public Task<List<Project>> GetForUserAsync(int userId, bool isAdmin) =>
        Accessible(userId, isAdmin)
            .Include(p => p.Workspace)
            .Include(p => p.Tasks)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public Task<Project?> GetByIdForUserAsync(int id, int userId, bool isAdmin) =>
        Accessible(userId, isAdmin)
            .Include(p => p.Workspace)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<ProjectDetailsViewModel?> GetDetailsAsync(int id, int userId, bool isAdmin)
    {
        var project = await Accessible(userId, isAdmin)
            .Include(p => p.Workspace)
            .Include(p => p.Pages)
            .Include(p => p.Tasks).ThenInclude(t => t.Assignee)
            .Include(p => p.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project is null) return null;

        // Hoạt động gần đây liên quan tới project và các task của nó.
        var entities = new List<(string, int)> { ("Project", project.Id) };
        entities.AddRange(project.Tasks.Select(t => ("Task", t.Id)));
        var activities = await _activity.GetForEntitiesAsync(entities);

        return new ProjectDetailsViewModel
        {
            Project = project,
            Pages = project.Pages.OrderByDescending(p => p.UpdatedAt).ToList(),
            Tasks = project.Tasks.OrderBy(t => t.Status).ThenBy(t => t.DueDate).ToList(),
            Members = project.Members.OrderByDescending(m => m.Role).ToList(),
            RecentActivities = activities,
            TotalTasks = project.Tasks.Count,
            CompletedTasks = project.Tasks.Count(t => t.Status == TaskStatus.Done)
        };
    }

    public Task<bool> HasWorkspaceAccessAsync(int workspaceId, int userId, bool isAdmin)
    {
        if (isAdmin) return _db.Workspaces.AnyAsync(w => w.Id == workspaceId);
        return _db.WorkspaceMembers.AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId);
    }

    public async Task<Project?> CreateAsync(ProjectFormViewModel model, int userId, bool isAdmin)
    {
        if (!await HasWorkspaceAccessAsync(model.WorkspaceId, userId, isAdmin))
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

    public async Task<bool> UpdateAsync(ProjectFormViewModel model, int userId, bool isAdmin)
    {
        var project = await GetByIdForUserAsync(model.Id, userId, isAdmin);
        if (project is null) return false;

        project.Name = model.Name.Trim();
        project.Description = model.Description?.Trim();
        await _db.SaveChangesAsync();

        await _activity.LogAsync(userId, "Updated", "Project", project.Id, $"Cập nhật project \"{project.Name}\"");
        return true;
    }

    public async Task<bool> DeleteAsync(int id, int userId, bool isAdmin)
    {
        var project = await GetByIdForUserAsync(id, userId, isAdmin);
        if (project is null) return false;

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
        return true;
    }
}
