using Cetee.Models;

namespace Cetee.ViewModels;

public class DashboardViewModel
{
    public int TotalWorkspaces { get; set; }
    public int TotalProjects { get; set; }
    public int TotalTasks { get; set; }

    public int TodoTasks { get; set; }
    public int DoingTasks { get; set; }
    public int DoneTasks { get; set; }
    public int OverdueTasks { get; set; }

    public IEnumerable<Project> RecentProjects { get; set; } = new List<Project>();
    public IEnumerable<TaskItem> UpcomingTasks { get; set; } = new List<TaskItem>();
    public IEnumerable<TaskItem> RecentTasks { get; set; } = new List<TaskItem>();
    public IEnumerable<ActivityLog> RecentActivities { get; set; } = new List<ActivityLog>();

    public EmployeeScopeResult? Scope { get; set; }

    /// <summary>Phần trăm task hoàn thành trên tổng số task.</summary>
    public int CompletionPercent => TotalTasks == 0 ? 0 : (int)Math.Round(DoneTasks * 100.0 / TotalTasks);
}
