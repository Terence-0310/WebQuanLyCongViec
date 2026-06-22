using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Cetee.Data;
using Cetee.Hubs;
using Cetee.Models;
using Cetee.ViewModels;
using TaskStatus = Cetee.Models.TaskStatus;

namespace Cetee.Services;

public interface ITaskService
{
    Task<List<TaskItem>> GetForUserAsync(int userId, bool seeAll, int? projectId, TaskStatus? status, string? search = null, IReadOnlyList<int>? assigneeFilter = null);
    Task<TaskItem?> GetByIdForUserAsync(int id, int userId, bool seeAll);
    Task<TaskDetailsViewModel?> GetDetailsAsync(int id, int userId, bool seeAll);
    Task<TaskItem?> CreateAsync(TaskFormViewModel model, int userId, bool seeAll);
    Task<bool> UpdateAsync(TaskFormViewModel model, int userId, bool seeAll);
    Task<int?> DeleteAsync(int id, int userId, bool seeAll);
    Task<bool> ChangeStatusAsync(int id, TaskStatus status, int userId, bool seeAll);
    Task<bool> AddCommentAsync(int taskId, int userId, bool seeAll, string content);
    Task<List<Project>> GetAccessibleProjectsAsync(int userId, bool seeAll);
    Task<List<User>> GetProjectMembersAsync(int projectId);

    Task<TimelineViewModel> GetTimelineAsync(int userId, bool seeAll, DateTime date, IReadOnlyList<int>? assigneeFilter = null);
    Task<WeekCalendarViewModel> GetWeekAsync(int userId, bool seeAll, DateTime date, IReadOnlyList<int>? assigneeFilter = null);
    Task<MonthCalendarViewModel> GetMonthAsync(int userId, bool seeAll, DateTime date, IReadOnlyList<int>? assigneeFilter = null);
    Task<bool> ScheduleAsync(int id, bool changeStart, DateTime? start, int? duration, int userId, bool seeAll);
}

/// <summary>Nghiệp vụ quản lý task: giao việc, đổi trạng thái, bình luận và ghi nhật ký.</summary>
public class TaskService : ITaskService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IActivityLogService _activity;
    private readonly IHubContext<RealtimeHub> _rt;

    public TaskService(AppDbContext db, INotificationService notifications, IActivityLogService activity,
        IHubContext<RealtimeHub> rt)
    {
        _db = db;
        _notifications = notifications;
        _activity = activity;
        _rt = rt;
    }

    // Báo cho mọi client đang xem rằng dữ liệu công việc vừa thay đổi (để tự đồng bộ).
    private Task BroadcastChangedAsync() => _rt.Clients.All.SendAsync("dataChanged", new { kind = "task" });

    private IQueryable<TaskItem> Accessible(int userId, bool seeAll)
    {
        var query = _db.Tasks.AsQueryable();
        if (seeAll) return query; // Chỉ SuperAdmin xem toàn bộ.

        // Người dùng thấy task: trong workspace mình tham gia, HOẶC được giao cho mình,
        // HOẶC được giao cho người trong đội mình (cấp dưới trực tiếp hoặc qua một cấp
        // trung gian — Manager thấy việc của User, Admin thấy việc của Manager lẫn User).
        // Với đa phụ trách: chỉ cần MỘT người phụ trách thỏa điều kiện là task hiển thị.
        return query.Where(t =>
            t.Project.Workspace.Members.Any(m => m.UserId == userId)
            || t.Assignees.Any(a =>
                    a.UserId == userId
                    || a.User.ManagerId == userId
                    || (a.User.Manager != null && a.User.Manager.ManagerId == userId)));
    }

    public async Task<List<TaskItem>> GetForUserAsync(int userId, bool seeAll, int? projectId, TaskStatus? status, string? search = null, IReadOnlyList<int>? assigneeFilter = null)
    {
        var query = Accessible(userId, seeAll)
            .Include(t => t.Project)
            .Include(t => t.Assignees).ThenInclude(a => a.User)
            .AsQueryable();

        if (projectId.HasValue) query = query.Where(t => t.ProjectId == projectId.Value);
        if (status.HasValue) query = query.Where(t => t.Status == status.Value);
        if (assigneeFilter != null)
            query = query.Where(t => t.Assignees.Any(a => assigneeFilter.Contains(a.UserId)));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(t => t.Title.Contains(term));
        }

        return await query
            .OrderBy(t => t.Status)
            .ThenBy(t => t.DueDate)
            .ToListAsync();
    }

    public Task<TaskItem?> GetByIdForUserAsync(int id, int userId, bool seeAll) =>
        Accessible(userId, seeAll)
            .Include(t => t.Project)
            .Include(t => t.Assignees).ThenInclude(a => a.User)
            .Include(t => t.Comments).ThenInclude(c => c.User)
            .FirstOrDefaultAsync(t => t.Id == id);

    public async Task<TaskDetailsViewModel?> GetDetailsAsync(int id, int userId, bool seeAll)
    {
        var task = await GetByIdForUserAsync(id, userId, seeAll);
        if (task is null) return null;

        var comments = task.Comments.OrderBy(c => c.CreatedAt).ToList();
        var activities = await _activity.GetForEntitiesAsync(new[] { ("Task", task.Id) });

        return new TaskDetailsViewModel
        {
            Task = task,
            Comments = comments,
            RecentActivities = activities
        };
    }

    public async Task<TaskItem?> CreateAsync(TaskFormViewModel model, int userId, bool seeAll)
    {
        bool hasAccess = seeAll
            ? await _db.Projects.AnyAsync(p => p.Id == model.ProjectId)
            : await _db.Projects.AnyAsync(p => p.Id == model.ProjectId && p.Workspace.Members.Any(m => m.UserId == userId));
        if (!hasAccess) return null;

        // Chỉ chấp nhận người thực hiện là thành viên của project (đa phụ trách).
        var assigneeIds = await ValidAssigneesAsync(model.ProjectId, model.AssigneeIds);

        // Mặc định: không chọn ai mà người tạo là thành viên project -> giao cho chính họ,
        // để task hiện ngay trong danh sách / Kanban / Lịch của họ và xếp lịch được.
        if (assigneeIds.Count == 0 &&
            await _db.ProjectMembers.AnyAsync(m => m.ProjectId == model.ProjectId && m.UserId == userId))
        {
            assigneeIds.Add(userId);
        }

        var task = new TaskItem
        {
            Title = model.Title.Trim(),
            Description = model.Description?.Trim(),
            ProjectId = model.ProjectId,
            Priority = model.Priority,
            Status = model.Status,
            DueDate = model.DueDate,
            DurationMinutes = model.DurationMinutes,
            Assignees = assigneeIds.Select(uid => new TaskAssignee { UserId = uid }).ToList()
        };
        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();

        await _activity.LogAsync(userId, "Created", "Task", task.Id, $"Tạo task \"{task.Title}\"");

        // Thông báo cho người được giao (trừ chính người tạo).
        foreach (var uid in assigneeIds.Where(id => id != userId))
            await _notifications.CreateAsync(uid, $"Bạn được giao task: {task.Title}", task.Id);

        await BroadcastChangedAsync();
        return task;
    }

    public async Task<bool> UpdateAsync(TaskFormViewModel model, int userId, bool seeAll)
    {
        var task = await Accessible(userId, seeAll)
            .Include(t => t.Assignees)
            .FirstOrDefaultAsync(t => t.Id == model.Id);
        if (task is null) return false;

        var previousAssignees = task.Assignees.Select(a => a.UserId).ToHashSet();
        var newAssignees = await ValidAssigneesAsync(task.ProjectId, model.AssigneeIds);

        task.Title = model.Title.Trim();
        task.Description = model.Description?.Trim();
        task.Priority = model.Priority;
        task.Status = model.Status;
        task.DueDate = model.DueDate;
        task.DurationMinutes = model.DurationMinutes;

        // Đồng bộ danh sách phụ trách: gỡ người bị bỏ, thêm người mới.
        task.Assignees.Clear();
        foreach (var uid in newAssignees)
            task.Assignees.Add(new TaskAssignee { TaskItemId = task.Id, UserId = uid });

        await _db.SaveChangesAsync();

        await _activity.LogAsync(userId, "Updated", "Task", task.Id, $"Cập nhật task \"{task.Title}\"");

        // Thông báo cho người MỚI được giao (không lặp lại với người đã có).
        foreach (var uid in newAssignees.Where(id => !previousAssignees.Contains(id)))
            await _notifications.CreateAsync(uid, $"Bạn được giao task: {task.Title}", task.Id);

        await BroadcastChangedAsync();
        return true;
    }

    /// <summary>Lọc danh sách người được giao về những người thực sự là thành viên project (loại trùng).</summary>
    private async Task<List<int>> ValidAssigneesAsync(int projectId, IEnumerable<int>? requested)
    {
        if (requested is null) return new List<int>();
        var wanted = requested.Distinct().ToHashSet();
        if (wanted.Count == 0) return new List<int>();

        return await _db.ProjectMembers
            .Where(m => m.ProjectId == projectId && wanted.Contains(m.UserId))
            .Select(m => m.UserId)
            .ToListAsync();
    }

    public async Task<int?> DeleteAsync(int id, int userId, bool seeAll)
    {
        var task = await Accessible(userId, seeAll).FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return null;

        int projectId = task.ProjectId;
        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync();
        await BroadcastChangedAsync();
        return projectId;
    }

    public async Task<bool> ChangeStatusAsync(int id, TaskStatus status, int userId, bool seeAll)
    {
        var task = await Accessible(userId, seeAll).FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return false;

        var oldStatus = task.Status;
        task.Status = status;
        await _db.SaveChangesAsync();

        await _activity.LogAsync(userId, "ChangedStatus", "Task", task.Id,
            $"Đổi trạng thái task \"{task.Title}\": {oldStatus.Label()} → {status.Label()}");
        await BroadcastChangedAsync();
        return true;
    }

    public async Task<bool> AddCommentAsync(int taskId, int userId, bool seeAll, string content)
    {
        var task = await Accessible(userId, seeAll).FirstOrDefaultAsync(t => t.Id == taskId);
        if (task is null) return false;

        _db.TaskComments.Add(new TaskComment
        {
            TaskItemId = taskId,
            UserId = userId,
            Content = content.Trim()
        });
        await _db.SaveChangesAsync();

        await _activity.LogAsync(userId, "Commented", "Task", taskId, $"Bình luận trong task \"{task.Title}\"");
        return true;
    }

    public Task<List<Project>> GetAccessibleProjectsAsync(int userId, bool seeAll)
    {
        var query = seeAll
            ? _db.Projects
            : _db.Projects.Where(p => p.Workspace.Members.Any(m => m.UserId == userId));

        return query.OrderBy(p => p.Name).ToListAsync();
    }

    public Task<List<User>> GetProjectMembersAsync(int projectId) =>
        _db.ProjectMembers
            .Where(m => m.ProjectId == projectId)
            .Select(m => m.User)
            .ToListAsync();

    public async Task<TimelineViewModel> GetTimelineAsync(int userId, bool seeAll, DateTime date, IReadOnlyList<int>? assigneeFilter = null)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        var baseQuery = Accessible(userId, seeAll)
            .Include(t => t.Project)
            .Include(t => t.Assignees).ThenInclude(a => a.User)
            .AsQueryable();

        // Lọc theo người được giao khi xem lịch của một nhân viên cụ thể.
        if (assigneeFilter != null)
            baseQuery = baseQuery.Where(t => t.Assignees.Any(a => assigneeFilter.Contains(a.UserId)));

        // Task đã xếp lịch trong ngày đang xem.
        var scheduled = await baseQuery
            .Where(t => t.ScheduledStart != null && t.ScheduledStart >= dayStart && t.ScheduledStart < dayEnd)
            .OrderBy(t => t.ScheduledStart)
            .ToListAsync();

        // Task chưa xếp lịch và chưa hoàn thành (để kéo vào timeline).
        var unscheduled = await baseQuery
            .Where(t => t.ScheduledStart == null && t.Status != TaskStatus.Done)
            .OrderBy(t => t.DueDate)
            .ToListAsync();

        return new TimelineViewModel { Date = dayStart, Scheduled = scheduled, Unscheduled = unscheduled };
    }

    // Thứ Hai đầu tuần chứa ngày d (tuần bắt đầu từ Thứ Hai theo thói quen VN).
    private static DateTime WeekStartOf(DateTime d)
    {
        int offset = ((int)d.DayOfWeek + 6) % 7; // Monday=0 ... Sunday=6
        return d.Date.AddDays(-offset);
    }

    // Task đã xếp lịch trong khoảng [start, end), kèm project + người phụ trách.
    private async Task<List<TaskItem>> ScheduledBetweenAsync(
        int userId, bool seeAll, DateTime start, DateTime end, IReadOnlyList<int>? assigneeFilter)
    {
        var query = Accessible(userId, seeAll)
            .Include(t => t.Project)
            .Include(t => t.Assignees).ThenInclude(a => a.User)
            .Where(t => t.ScheduledStart != null && t.ScheduledStart >= start && t.ScheduledStart < end);

        if (assigneeFilter != null)
            query = query.Where(t => t.Assignees.Any(a => assigneeFilter.Contains(a.UserId)));

        return await query.OrderBy(t => t.ScheduledStart).ToListAsync();
    }

    public async Task<WeekCalendarViewModel> GetWeekAsync(int userId, bool seeAll, DateTime date, IReadOnlyList<int>? assigneeFilter = null)
    {
        var start = WeekStartOf(date);
        var end = start.AddDays(7);
        var tasks = await ScheduledBetweenAsync(userId, seeAll, start, end, assigneeFilter);

        var days = Enumerable.Range(0, 7).Select(i =>
        {
            var day = start.AddDays(i);
            return new CalendarDay
            {
                Date = day,
                Tasks = tasks.Where(t => t.ScheduledStart!.Value.Date == day).ToList()
            };
        }).ToList();

        return new WeekCalendarViewModel
        {
            WeekStart = start,
            Days = days,
            IsThisWeek = start == WeekStartOf(DateTime.Today)
        };
    }

    public async Task<MonthCalendarViewModel> GetMonthAsync(int userId, bool seeAll, DateTime date, IReadOnlyList<int>? assigneeFilter = null)
    {
        var monthStart = new DateTime(date.Year, date.Month, 1);
        var gridStart = WeekStartOf(monthStart);   // lùi về Thứ Hai để lấp đầy lưới
        var gridEnd = gridStart.AddDays(42);        // 6 tuần × 7 ngày
        var tasks = await ScheduledBetweenAsync(userId, seeAll, gridStart, gridEnd, assigneeFilter);

        var days = Enumerable.Range(0, 42).Select(i =>
        {
            var day = gridStart.AddDays(i);
            return new CalendarDay
            {
                Date = day,
                InMonth = day.Month == monthStart.Month && day.Year == monthStart.Year,
                Tasks = tasks.Where(t => t.ScheduledStart!.Value.Date == day).ToList()
            };
        }).ToList();

        var today = DateTime.Today;
        return new MonthCalendarViewModel
        {
            MonthStart = monthStart,
            Days = days,
            IsThisMonth = monthStart == new DateTime(today.Year, today.Month, 1)
        };
    }

    public async Task<bool> ScheduleAsync(int id, bool changeStart, DateTime? start, int? duration, int userId, bool seeAll)
    {
        var task = await Accessible(userId, seeAll).FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return false;

        if (changeStart) task.ScheduledStart = start;
        if (duration.HasValue) task.DurationMinutes = Math.Clamp(duration.Value, 5, 480);
        await _db.SaveChangesAsync();

        // Chỉ ghi nhật ký khi thực sự xếp/đổi giờ.
        if (changeStart && start.HasValue)
            await _activity.LogAsync(userId, "Scheduled", "Task", task.Id,
                $"Xếp lịch task \"{task.Title}\" lúc {start.Value:HH:mm dd/MM} ({task.DurationMinutes} phút)");
        await BroadcastChangedAsync();
        return true;
    }
}
