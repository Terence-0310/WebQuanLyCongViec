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
    public List<int> AssigneeIds { get; set; } = new();

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
    public EmployeeScopeResult? Scope { get; set; }
}

/// <summary>Dữ liệu cho bảng Kanban: nhóm task theo cột trạng thái.</summary>
public class KanbanViewModel
{
    public int? ProjectId { get; set; }
    public IEnumerable<SelectListItem> ProjectOptions { get; set; } = new List<SelectListItem>();
    public List<TaskItem> Todo { get; set; } = new();
    public List<TaskItem> Doing { get; set; } = new();
    public List<TaskItem> Done { get; set; } = new();
    public EmployeeScopeResult? Scope { get; set; }
}

/// <summary>Dữ liệu cho trang Timeline (lịch ngày): task đã xếp lịch và chưa xếp lịch.</summary>
public class TimelineViewModel
{
    public DateTime Date { get; set; }
    public List<TaskItem> Scheduled { get; set; } = new();   // có ScheduledStart trong ngày
    public List<TaskItem> Unscheduled { get; set; } = new();  // chưa xếp lịch, chưa hoàn thành
    public EmployeeScopeResult? Scope { get; set; }

    public DateTime PrevDate => Date.AddDays(-1);
    public DateTime NextDate => Date.AddDays(1);
    public bool IsToday => Date.Date == DateTime.Today;
}

/// <summary>Một ô ngày trên lịch tuần/tháng: ngày + các task đã xếp lịch trong ngày đó.</summary>
public class CalendarDay
{
    public DateTime Date { get; set; }
    public List<TaskItem> Tasks { get; set; } = new();

    /// <summary>Thuộc tháng đang xem (lịch tháng có vài ô của tháng trước/sau để lấp lưới).</summary>
    public bool InMonth { get; set; } = true;

    public bool IsToday => Date.Date == DateTime.Today;
    public int Total => Tasks.Count;
    public int Done => Tasks.Count(t => t.Status == TaskStatus.Done);
    public int DonePercent => Total == 0 ? 0 : (int)Math.Round(Done * 100.0 / Total);
}

/// <summary>Cơ sở cho lịch tuần/tháng: tổng hợp số liệu hoàn thành để "đếm công".</summary>
public abstract class CalendarViewModel
{
    public List<CalendarDay> Days { get; set; } = new();
    public EmployeeScopeResult? Scope { get; set; }

    // Chỉ tính các ngày thuộc phạm vi đang xem (lịch tháng bỏ qua ngày tháng khác).
    public int TotalScheduled => Days.Where(d => d.InMonth).Sum(d => d.Total);
    public int TotalDone => Days.Where(d => d.InMonth).Sum(d => d.Done);
    public int CompletionPercent => TotalScheduled == 0 ? 0 : (int)Math.Round(TotalDone * 100.0 / TotalScheduled);
}

/// <summary>Lịch tuần (7 ngày, bắt đầu từ Thứ Hai).</summary>
public class WeekCalendarViewModel : CalendarViewModel
{
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd => WeekStart.AddDays(6);
    public DateTime PrevWeek => WeekStart.AddDays(-7);
    public DateTime NextWeek => WeekStart.AddDays(7);
    public bool IsThisWeek { get; set; }
}

/// <summary>Lịch tháng (lưới 6 tuần × 7 ngày).</summary>
public class MonthCalendarViewModel : CalendarViewModel
{
    public DateTime MonthStart { get; set; }
    public DateTime PrevMonth => MonthStart.AddMonths(-1);
    public DateTime NextMonth => MonthStart.AddMonths(1);
    public bool IsThisMonth { get; set; }

    /// <summary>Chia 42 ô thành 6 hàng tuần.</summary>
    public IEnumerable<CalendarDay[]> Weeks => Days.Chunk(7);
}
