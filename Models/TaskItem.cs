using System.ComponentModel.DataAnnotations;

namespace Cetee.Models;

/// <summary>Công việc (task) trong project. Đặt tên TaskItem để tránh trùng System.Threading.Tasks.Task.</summary>
public class TaskItem
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public TaskStatus Status { get; set; } = TaskStatus.Todo;

    public DateTime? DueDate { get; set; }

    // Thời điểm bắt đầu được xếp lịch trên timeline trong ngày (null = chưa xếp lịch).
    public DateTime? ScheduledStart { get; set; }

    // Thời lượng dự kiến (phút), dùng để vẽ độ cao khối trên timeline.
    public int DurationMinutes { get; set; } = 60;

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    // Người được giao task (có thể chưa giao).
    public int? AssigneeId { get; set; }
    public User? Assignee { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();

    /// <summary>Task đã quá hạn: có deadline, chưa hoàn thành và deadline ở quá khứ.</summary>
    public bool IsOverdue =>
        DueDate.HasValue && Status != TaskStatus.Done && DueDate.Value.Date < DateTime.UtcNow.Date;
}
