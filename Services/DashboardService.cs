using Microsoft.EntityFrameworkCore;
using Cetee.Data;
using Cetee.ViewModels;
using TaskStatus = Cetee.Models.TaskStatus;

namespace Cetee.Services;

public interface IDashboardService
{
    /// <summary>Tổng hợp Dashboard. employeeId != null = xem thống kê của một nhân viên cụ thể.</summary>
    Task<DashboardViewModel> GetForUserAsync(int viewerId, bool isAdmin, int? employeeId = null);
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

    public async Task<DashboardViewModel> GetForUserAsync(int viewerId, bool isAdmin, int? employeeId = null)
    {
        IQueryable<Cetee.Models.Workspace> workspaces;
        IQueryable<Cetee.Models.Project> projects;
        IQueryable<Cetee.Models.TaskItem> tasks;

        if (employeeId is int emp)
        {
            // Thống kê của riêng một nhân viên: workspace/project họ tham gia, task được giao cho họ.
            workspaces = _db.Workspaces.Where(w => w.Members.Any(m => m.UserId == emp));
            projects = _db.Projects.Where(p => p.Workspace.Members.Any(m => m.UserId == emp));
            tasks = _db.Tasks.Where(t => t.AssigneeId == emp);
        }
        else if (isAdmin)
        {
            workspaces = _db.Workspaces;
            projects = _db.Projects;
            tasks = _db.Tasks;
        }
        else
        {
            // Manager/User xem "Tất cả" trong phạm vi của mình (workspace tham gia + việc nhân viên trực thuộc).
            workspaces = _db.Workspaces.Where(w => w.Members.Any(m => m.UserId == viewerId));
            projects = _db.Projects.Where(p => p.Workspace.Members.Any(m => m.UserId == viewerId));
            tasks = _db.Tasks.Where(t =>
                t.Project.Workspace.Members.Any(m => m.UserId == viewerId)
                || t.AssigneeId == viewerId
                || (t.Assignee != null && t.Assignee.ManagerId == viewerId));
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
                .Include(t => t.Assignee)
                .OrderByDescending(t => t.CreatedAt)
                .Take(6)
                .ToListAsync(),

            RecentActivities = await _activity.GetRecentAsync(employeeId ?? viewerId, isAdmin && employeeId is null)
        };
    }
}
