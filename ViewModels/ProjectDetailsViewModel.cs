using Cetee.Models;

namespace Cetee.ViewModels;

public class ProjectDetailsViewModel
{
    public Project Project { get; set; } = null!;
    public IEnumerable<Page> Pages { get; set; } = new List<Page>();
    public IEnumerable<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public IEnumerable<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public IEnumerable<ActivityLog> RecentActivities { get; set; } = new List<ActivityLog>();

    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }

    /// <summary>Tiến độ theo phần trăm task hoàn thành.</summary>
    public int ProgressPercent => TotalTasks == 0 ? 0 : (int)Math.Round(CompletedTasks * 100.0 / TotalTasks);
}
