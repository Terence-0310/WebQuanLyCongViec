using Microsoft.EntityFrameworkCore;
using Cetee.Data;
using Cetee.ViewModels;
using TaskStatus = Cetee.Models.TaskStatus;

namespace Cetee.Services;

public interface IDashboardService
{
    /// <summary>Tổng hợp Dashboard cho một tập người dùng trong phạm vi xem.
    /// scopeUserIds = null nghĩa là toàn hệ thống (chỉ SuperAdmin); ngược lại chỉ tính
    /// dữ liệu của những người trong tập (một đội, hoặc một cá nhân).</summary>
    Task<DashboardViewModel> GetForScopeAsync(IReadOnlyList<int>? scopeUserIds);
}

/// <summary>Tổng hợp số liệu cho trang Dashboard.</summary>
public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;
    private readonly IActivityLogService _activity;

    public DashboardService(AppDbContext db, IActivityLogService activity)
    {
        _db = db;
        _activity = activity;
    }

    public async Task<DashboardViewModel> GetForScopeAsync(IReadOnlyList<int>? scopeUserIds)
    {
        IQueryable<Cetee.Models.Workspace> workspaces;
        IQueryable<Cetee.Models.Project> projects;
        IQueryable<Cetee.Models.TaskItem> tasks;

        if (scopeUserIds is null)
        {
            // Toàn hệ thống (SuperAdmin).
            workspaces = _db.Workspaces;
            projects = _db.Projects;
            tasks = _db.Tasks;
        }
        else
        {
            // Một đội hoặc một cá nhân: workspace/project nhóm tham gia, task giao cho nhóm.
            workspaces = _db.Workspaces.Where(w => w.Members.Any(m => scopeUserIds.Contains(m.UserId)));
            projects = _db.Projects.Where(p => p.Workspace.Members.Any(m => scopeUserIds.Contains(m.UserId)));
            tasks = _db.Tasks.Where(t => t.Assignees.Any(a => scopeUserIds.Contains(a.UserId)));
        }

        var today = DateTime.UtcNow.Date;

        return new DashboardViewModel
        {
            TotalWorkspaces = await workspaces.CountAsync(),
            TotalProjects = await projects.CountAsync(),
            TotalTasks = await tasks.CountAsync(),

            TodoTasks = await tasks.CountAsync(t => t.Status == TaskStatus.Todo),
            DoingTasks = await tasks.CountAsync(t => t.Status == TaskStatus.Doing),
            DoneTasks = await tasks.CountAsync(t => t.Status == TaskStatus.Done),
            OverdueTasks = await tasks.CountAsync(t =>
                t.DueDate != null && t.Status != TaskStatus.Done && t.DueDate < today),

            RecentProjects = await projects
                .Include(p => p.Workspace)
                .Include(p => p.Tasks)
                .Include(p => p.Members)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync(),

            UpcomingTasks = await tasks
                .Include(t => t.Project)
                .Where(t => t.Status != TaskStatus.Done && t.DueDate != null && t.DueDate >= today)
                .OrderBy(t => t.DueDate)
                .Take(5)
                .ToListAsync(),

            RecentTasks = await tasks
                .Include(t => t.Project)
                .Include(t => t.Assignees).ThenInclude(a => a.User)
                .OrderByDescending(t => t.CreatedAt)
                .Take(6)
                .ToListAsync(),

            RecentActivities = await _activity.GetRecentForScopeAsync(scopeUserIds)
        };
    }
}
