using Microsoft.EntityFrameworkCore;
using Cetee.Data;
using Cetee.ViewModels;
using TaskStatus = Cetee.Models.TaskStatus;

namespace Cetee.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> GetForUserAsync(int userId, bool isAdmin);
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

    public async Task<DashboardViewModel> GetForUserAsync(int userId, bool isAdmin)
    {
        // Giới hạn dữ liệu theo quyền: Admin xem tất cả, user chỉ xem phạm vi mình tham gia.
        var workspaces = isAdmin
            ? _db.Workspaces
            : _db.Workspaces.Where(w => w.Members.Any(m => m.UserId == userId));
        var projects = isAdmin
            ? _db.Projects
            : _db.Projects.Where(p => p.Workspace.Members.Any(m => m.UserId == userId));
        var tasks = isAdmin
            ? _db.Tasks
            : _db.Tasks.Where(t => t.Project.Workspace.Members.Any(m => m.UserId == userId));

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
                .Include(t => t.Assignee)
                .OrderByDescending(t => t.CreatedAt)
                .Take(6)
                .ToListAsync(),

            RecentActivities = await _activity.GetRecentAsync(userId, isAdmin)
        };
    }
}
