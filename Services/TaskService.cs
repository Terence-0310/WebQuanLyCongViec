using Microsoft.EntityFrameworkCore;
using Cetee.Data;
using Cetee.Models;
using Cetee.ViewModels;
using TaskStatus = Cetee.Models.TaskStatus;

namespace Cetee.Services;

public interface ITaskService
{
    Task<List<TaskItem>> GetForUserAsync(int userId, bool isAdmin, int? projectId, TaskStatus? status, string? search = null, IReadOnlyList<int>? assigneeFilter = null);
    Task<TaskItem?> GetByIdForUserAsync(int id, int userId, bool isAdmin);
    Task<TaskDetailsViewModel?> GetDetailsAsync(int id, int userId, bool isAdmin);
    Task<(TaskItem? Task, string? Error)> CreateAsync(TaskFormViewModel model, int userId, bool isAdmin);
    Task<(bool Ok, string? Error)> UpdateAsync(TaskFormViewModel model, int userId, bool isAdmin);
    Task<int?> DeleteAsync(int id, int userId, bool isAdmin);
    Task<bool> ChangeStatusAsync(int id, TaskStatus status, int userId, bool isAdmin);
    Task<bool> AddCommentAsync(int taskId, int userId, bool isAdmin, string content);
    Task<List<Project>> GetAccessibleProjectsAsync(int userId, bool isAdmin);
    Task<List<User>> GetProjectMembersAsync(int projectId);

    Task<TimelineViewModel> GetTimelineAsync(int userId, bool isAdmin, DateTime date, IReadOnlyList<int>? assigneeFilter = null);
    Task<bool> ScheduleAsync(int id, bool changeStart, DateTime? start, int? duration, int userId, bool isAdmin, int? employeeId = null);
}

/// <summary>Nghiệp vụ quản lý task: giao việc, đổi trạng thái, bình luận và ghi nhật ký.</summary>
public class TaskService : ITaskService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IActivityLogService _activity;

    public TaskService(AppDbContext db, INotificationService notifications, IActivityLogService activity)
    {
        _db = db;
        _notifications = notifications;
        _activity = activity;
    }

    private IQueryable<TaskItem> Accessible(int userId, bool isAdmin)
    {
        var query = _db.Tasks.AsQueryable();
        if (isAdmin) return query;

        // User thường thấy task: trong workspace mình tham gia, HOẶC được giao cho mình,
        // HOẶC được giao cho nhân viên trực thuộc mình (quản lý xem việc của nhân viên).
        return query.Where(t =>
            t.Project.Workspace.Members.Any(m => m.UserId == userId)
            || t.AssigneeId == userId
            || (t.Assignee != null && t.Assignee.ManagerId == userId));
    }

    public async Task<List<TaskItem>> GetForUserAsync(int userId, bool isAdmin, int? projectId, TaskStatus? status, string? search = null, IReadOnlyList<int>? assigneeFilter = null)
    {
        var query = Accessible(userId, isAdmin)
            .Include(t => t.Project)
            .Include(t => t.Assignee)
            .AsQueryable();

        if (projectId.HasValue) query = query.Where(t => t.ProjectId == projectId.Value);
        if (status.HasValue) query = query.Where(t => t.Status == status.Value);
        if (assigneeFilter != null)
            query = query.Where(t => t.AssigneeId != null && assigneeFilter.Contains(t.AssigneeId.Value));
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

    public Task<TaskItem?> GetByIdForUserAsync(int id, int userId, bool isAdmin) =>
        Accessible(userId, isAdmin)
            .Include(t => t.Project)
            .Include(t => t.Assignee)
            .Include(t => t.Comments).ThenInclude(c => c.User)
            .FirstOrDefaultAsync(t => t.Id == id);

    public async Task<TaskDetailsViewModel?> GetDetailsAsync(int id, int userId, bool isAdmin)
    {
        var task = await GetByIdForUserAsync(id, userId, isAdmin);
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

    private async Task<(int? AssigneeId, string? Error)> ResolveAssigneeEmailAsync(string? email, int projectId)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (null, null);

        var trimmedEmail = email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == trimmedEmail);
        if (user == null)
            return (null, "Email người thực hiện không tồn tại trong hệ thống.");

        // Kiểm tra và tự động thêm vào project member (nếu chưa tham gia)
        bool isProjectMember = await _db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == user.Id);
        if (!isProjectMember)
        {
            _db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = projectId,
                UserId = user.Id,
                Role = MemberRole.Member
            });
        }

        // Kiểm tra và tự động thêm vào workspace member (nếu chưa tham gia)
        var project = await _db.Projects.FindAsync(projectId);
        if (project != null)
        {
            bool isWorkspaceMember = await _db.WorkspaceMembers.AnyAsync(m => m.WorkspaceId == project.WorkspaceId && m.UserId == user.Id);
            if (!isWorkspaceMember)
            {
                _db.WorkspaceMembers.Add(new WorkspaceMember
                {
                    WorkspaceId = project.WorkspaceId,
                    UserId = user.Id,
                    Role = MemberRole.Member
                });
            }
        }

        return (user.Id, null);
    }

    public async Task<(TaskItem? Task, string? Error)> CreateAsync(TaskFormViewModel model, int userId, bool isAdmin)
    {
        bool hasAccess = isAdmin
            ? await _db.Projects.AnyAsync(p => p.Id == model.ProjectId)
            : await _db.Projects.AnyAsync(p => p.Id == model.ProjectId && p.Workspace.Members.Any(m => m.UserId == userId));
        if (!hasAccess) return (null, null);

        var (assigneeId, error) = await ResolveAssigneeEmailAsync(model.AssigneeEmail, model.ProjectId);
        if (error != null) return (null, error);

        var task = new TaskItem
        {
            Title = model.Title.Trim(),
            Description = model.Description?.Trim(),
            ProjectId = model.ProjectId,
            AssigneeId = assigneeId,
            Priority = model.Priority,
            Status = model.Status,
            DueDate = model.DueDate,
            DurationMinutes = model.DurationMinutes
        };
        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();

        await _activity.LogAsync(userId, "Created", "Task", task.Id, $"Tạo task \"{task.Title}\"");

        // Tạo thông báo nếu task được giao cho ai đó.
        if (task.AssigneeId.HasValue)
            await _notifications.CreateAsync(task.AssigneeId.Value, $"Bạn được giao task: {task.Title}", task.Id);

        return (task, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(TaskFormViewModel model, int userId, bool isAdmin)
    {
        var task = await Accessible(userId, isAdmin).FirstOrDefaultAsync(t => t.Id == model.Id);
        if (task is null) return (false, null);

        var (assigneeId, error) = await ResolveAssigneeEmailAsync(model.AssigneeEmail, task.ProjectId);
        if (error != null) return (false, error);

        int? previousAssignee = task.AssigneeId;

        task.Title = model.Title.Trim();
        task.Description = model.Description?.Trim();
        task.AssigneeId = assigneeId;
        task.Priority = model.Priority;
        task.Status = model.Status;
        task.DueDate = model.DueDate;
        task.DurationMinutes = model.DurationMinutes;
        await _db.SaveChangesAsync();

        await _activity.LogAsync(userId, "Updated", "Task", task.Id, $"Cập nhật task \"{task.Title}\"");

        // Nếu đổi người thực hiện sang người mới thì thông báo.
        if (task.AssigneeId.HasValue && task.AssigneeId != previousAssignee)
            await _notifications.CreateAsync(task.AssigneeId.Value, $"Bạn được giao task: {task.Title}", task.Id);

        return (true, null);
    }

    public async Task<int?> DeleteAsync(int id, int userId, bool isAdmin)
    {
        var task = await Accessible(userId, isAdmin).FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return null;

        int projectId = task.ProjectId;
        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync();
        return projectId;
    }

    public async Task<bool> ChangeStatusAsync(int id, TaskStatus status, int userId, bool isAdmin)
    {
        var task = await Accessible(userId, isAdmin).FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return false;

        var oldStatus = task.Status;
        task.Status = status;
        await _db.SaveChangesAsync();

        await _activity.LogAsync(userId, "ChangedStatus", "Task", task.Id,
            $"Đổi trạng thái task \"{task.Title}\": {oldStatus.Label()} → {status.Label()}");
        return true;
    }

    public async Task<bool> AddCommentAsync(int taskId, int userId, bool isAdmin, string content)
    {
        var task = await Accessible(userId, isAdmin).FirstOrDefaultAsync(t => t.Id == taskId);
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

    public Task<List<Project>> GetAccessibleProjectsAsync(int userId, bool isAdmin)
    {
        var query = isAdmin
            ? _db.Projects
            : _db.Projects.Where(p => p.Workspace.Members.Any(m => m.UserId == userId));

        return query.OrderBy(p => p.Name).ToListAsync();
    }

    public Task<List<User>> GetProjectMembersAsync(int projectId) =>
        _db.ProjectMembers
            .Where(m => m.ProjectId == projectId)
            .Select(m => m.User)
            .ToListAsync();

    public async Task<TimelineViewModel> GetTimelineAsync(int userId, bool isAdmin, DateTime date, IReadOnlyList<int>? assigneeFilter = null)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        var baseQuery = Accessible(userId, isAdmin)
            .Include(t => t.Project)
            .Include(t => t.Assignee)
            .AsQueryable();

        // Lọc theo người được giao khi xem lịch của một nhân viên cụ thể.
        var scheduledQuery = baseQuery;
        if (assigneeFilter != null)
        {
            scheduledQuery = scheduledQuery.Where(t => t.AssigneeId != null && assigneeFilter.Contains(t.AssigneeId.Value));
        }

        // Task đã xếp lịch trong ngày đang xem.
        var scheduled = await scheduledQuery
            .Where(t => t.ScheduledStart != null && t.ScheduledStart >= dayStart && t.ScheduledStart < dayEnd)
            .OrderBy(t => t.ScheduledStart)
            .ToListAsync();

        // Task chưa xếp lịch và chưa hoàn thành (để kéo vào timeline).
        var unscheduledQuery = baseQuery.Where(t => t.ScheduledStart == null && t.Status != TaskStatus.Done);
        if (assigneeFilter != null)
        {
            unscheduledQuery = unscheduledQuery.Where(t => 
                (t.AssigneeId != null && assigneeFilter.Contains(t.AssigneeId.Value)) 
                || t.AssigneeId == null);
        }

        var unscheduled = await unscheduledQuery
            .OrderBy(t => t.DueDate)
            .ToListAsync();

        return new TimelineViewModel { Date = dayStart, Scheduled = scheduled, Unscheduled = unscheduled };
    }

    public async Task<bool> ScheduleAsync(int id, bool changeStart, DateTime? start, int? duration, int userId, bool isAdmin, int? employeeId = null)
    {
        var task = await Accessible(userId, isAdmin).FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return false;

        if (changeStart) task.ScheduledStart = start;
        if (duration.HasValue) task.DurationMinutes = Math.Clamp(duration.Value, 5, 480);

        // Nếu kéo vào lịch của một nhân viên cụ thể, tự động giao task cho nhân viên đó.
        if (employeeId.HasValue && employeeId.Value > 0)
        {
            var oldAssignee = task.AssigneeId;
            task.AssigneeId = employeeId.Value;

            // Đảm bảo họ là thành viên của project/workspace.
            var assigneeUser = await _db.Users.FindAsync(employeeId.Value);
            if (assigneeUser != null && oldAssignee != employeeId.Value)
            {
                // Thêm vào project member
                bool isProjectMember = await _db.ProjectMembers.AnyAsync(m => m.ProjectId == task.ProjectId && m.UserId == employeeId.Value);
                if (!isProjectMember)
                {
                    _db.ProjectMembers.Add(new ProjectMember { ProjectId = task.ProjectId, UserId = employeeId.Value, Role = MemberRole.Member });
                }

                // Thêm vào workspace member
                var project = await _db.Projects.FindAsync(task.ProjectId);
                if (project != null)
                {
                    bool isWorkspaceMember = await _db.WorkspaceMembers.AnyAsync(m => m.WorkspaceId == project.WorkspaceId && m.UserId == employeeId.Value);
                    if (!isWorkspaceMember)
                    {
                        _db.WorkspaceMembers.Add(new WorkspaceMember { WorkspaceId = project.WorkspaceId, UserId = employeeId.Value, Role = MemberRole.Member });
                    }
                }

                // Gửi thông báo
                await _notifications.CreateAsync(employeeId.Value, $"Bạn được giao task qua lịch: {task.Title}", task.Id);
            }
        }

        await _db.SaveChangesAsync();

        // Chỉ ghi nhật ký khi thực sự xếp/đổi giờ.
        if (changeStart && start.HasValue)
            await _activity.LogAsync(userId, "Scheduled", "Task", task.Id,
                $"Xếp lịch task \"{task.Title}\" lúc {start.Value:HH:mm dd/MM} ({task.DurationMinutes} phút)");
        return true;
    }
}
