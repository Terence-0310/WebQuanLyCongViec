using System.ComponentModel.DataAnnotations;
using Cetee.Models;

namespace Cetee.ViewModels;

public class TaskDetailsViewModel
{
    public TaskItem Task { get; set; } = null!;
    public IEnumerable<TaskComment> Comments { get; set; } = new List<TaskComment>();
    public IEnumerable<ActivityLog> RecentActivities { get; set; } = new List<ActivityLog>();

    [Required(ErrorMessage = "Vui lòng nhập nội dung bình luận.")]
    [StringLength(1000)]
    [Display(Name = "Bình luận")]
    public string NewComment { get; set; } = string.Empty;
}
