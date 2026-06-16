using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Cetee.Models;
using TaskStatus = Cetee.Models.TaskStatus;

namespace Cetee.ViewModels;

public class TaskFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề task.")]
    [StringLength(200)]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Required]
    [Display(Name = "Project")]
    public int ProjectId { get; set; }

    [Display(Name = "Người thực hiện")]
    public int? AssigneeId { get; set; }

    [Display(Name = "Mức ưu tiên")]
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    [Display(Name = "Trạng thái")]
    public TaskStatus Status { get; set; } = TaskStatus.Todo;

    [DataType(DataType.Date)]
    [Display(Name = "Hạn chót")]
    public DateTime? DueDate { get; set; }

    [Range(5, 480, ErrorMessage = "Thời lượng từ 5 đến 480 phút.")]
    [Display(Name = "Thời lượng (phút)")]
    public int DurationMinutes { get; set; } = 60;

    // Dữ liệu hỗ trợ dropdown (không bắt buộc khi submit).
    public IEnumerable<SelectListItem> ProjectOptions { get; set; } = new List<SelectListItem>();
    public IEnumerable<SelectListItem> AssigneeOptions { get; set; } = new List<SelectListItem>();
}

/// <summary>Bộ lọc cho trang danh sách task.</summary>
public class TaskListViewModel
{
    public IEnumerable<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public int? ProjectId { get; set; }
    public TaskStatus? Status { get; set; }
    public string? Search { get; set; }
    public IEnumerable<SelectListItem> ProjectOptions { get; set; } = new List<SelectListItem>();
}

/// <summary>Dữ liệu cho bảng Kanban: nhóm task theo cột trạng thái.</summary>
public class KanbanViewModel
{
    public int? ProjectId { get; set; }
    public IEnumerable<SelectListItem> ProjectOptions { get; set; } = new List<SelectListItem>();
    public List<TaskItem> Todo { get; set; } = new();
    public List<TaskItem> Doing { get; set; } = new();
    public List<TaskItem> Done { get; set; } = new();
}

/// <summary>Dữ liệu cho trang Timeline (lịch ngày): task đã xếp lịch và chưa xếp lịch.</summary>
public class TimelineViewModel
{
    public DateTime Date { get; set; }
    public List<TaskItem> Scheduled { get; set; } = new();   // có ScheduledStart trong ngày
    public List<TaskItem> Unscheduled { get; set; } = new();  // chưa xếp lịch, chưa hoàn thành

    public DateTime PrevDate => Date.AddDays(-1);
    public DateTime NextDate => Date.AddDays(1);
    public bool IsToday => Date.Date == DateTime.Today;
}
