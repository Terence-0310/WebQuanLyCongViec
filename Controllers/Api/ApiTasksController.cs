using Microsoft.AspNetCore.Mvc;
using Cetee.Services;
using Cetee.ViewModels;
using TaskStatus = Cetee.Models.TaskStatus;

namespace Cetee.Controllers.Api;

[Route("api/tasks")]
public class ApiTasksController : ApiBaseController
{
    private readonly ITaskService _tasks;
    private readonly IUserService _users;

    public ApiTasksController(ITaskService tasks, IUserService users)
    {
        _tasks = tasks;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> GetTasks(
        [FromQuery] int? projectId,
        [FromQuery] TaskStatus? status,
        [FromQuery] string? q,
        [FromQuery] int? employeeId)
    {
        var scope = await _users.ResolveScopeAsync(CurrentUserId, CurrentRole, employeeId);
        var tasks = await _tasks.GetForUserAsync(CurrentUserId, IsAdmin, projectId, status, q, scope.AssigneeFilter);
        
        var result = tasks.Select(t => new
        {
            t.Id,
            t.Title,
            t.Description,
            t.ProjectId,
            projectName = t.Project.Name,
            t.AssigneeId,
            assigneeName = t.Assignee?.FullName,
            priority = t.Priority.ToString(),
            status = t.Status.ToString(),
            t.DueDate,
            t.DurationMinutes,
            t.ScheduledStart,
            t.CreatedAt
        });

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDetails(int id)
    {
        var details = await _tasks.GetDetailsAsync(id, CurrentUserId, IsAdmin);
        if (details is null)
            return NotFound(new { message = "Không tìm thấy công việc hoặc bạn không có quyền truy cập." });

        return Ok(new
        {
            task = new
            {
                details.Task.Id,
                details.Task.Title,
                details.Task.Description,
                details.Task.ProjectId,
                projectName = details.Task.Project.Name,
                details.Task.AssigneeId,
                assigneeName = details.Task.Assignee?.FullName,
                priority = details.Task.Priority.ToString(),
                status = details.Task.Status.ToString(),
                details.Task.DueDate,
                details.Task.DurationMinutes,
                details.Task.ScheduledStart,
                details.Task.CreatedAt
            },
            comments = details.Comments.Select(c => new
            {
                c.Id,
                c.UserId,
                userName = c.User.FullName,
                c.Content,
                c.CreatedAt
            }),
            recentActivities = details.RecentActivities.Select(a => new
            {
                a.Id,
                a.UserId,
                userName = a.User.FullName,
                action = a.Action,
                description = a.Description,
                a.CreatedAt
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TaskFormViewModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (model.DueDate.HasValue && model.DueDate.Value.Date < DateTime.UtcNow.Date)
            return BadRequest(new { message = "Hạn chót không được nhỏ hơn ngày hiện tại." });

        var (task, error) = await _tasks.CreateAsync(model, CurrentUserId, IsAdmin);
        if (error != null)
            return BadRequest(new { message = error });
        if (task is null)
            return Forbid();

        return CreatedAtAction(nameof(GetDetails), new { id = task.Id }, new { message = "Tạo công việc thành công.", id = task.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] TaskFormViewModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        model.Id = id;
        var (success, error) = await _tasks.UpdateAsync(model, CurrentUserId, IsAdmin);
        if (error != null)
            return BadRequest(new { message = error });
        if (!success)
            return NotFound(new { message = "Không tìm thấy công việc hoặc bạn không có quyền cập nhật." });

        return Ok(new { message = "Cập nhật công việc thành công." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _tasks.DeleteAsync(id, CurrentUserId, IsAdmin);
        if (result is null)
            return NotFound(new { message = "Không tìm thấy công việc hoặc bạn không có quyền xóa." });

        return Ok(new { message = "Xóa công việc thành công." });
    }

    [HttpPost("{id}/status")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeStatusInput input)
    {
        var success = await _tasks.ChangeStatusAsync(id, input.Status, CurrentUserId, IsAdmin);
        if (!success)
            return NotFound(new { message = "Không tìm thấy công việc hoặc bạn không có quyền thay đổi trạng thái." });

        return Ok(new { message = "Đổi trạng thái công việc thành công." });
    }

    [HttpPost("{id}/comments")]
    public async Task<IActionResult> AddComment(int id, [FromBody] AddCommentInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Comment))
            return BadRequest(new { message = "Nội dung bình luận không được để trống." });

        var success = await _tasks.AddCommentAsync(id, CurrentUserId, IsAdmin, input.Comment);
        if (!success)
            return NotFound(new { message = "Không tìm thấy công việc hoặc bạn không có quyền thêm bình luận." });

        return Ok(new { message = "Thêm bình luận thành công." });
    }
}

public class ChangeStatusInput
{
    public TaskStatus Status { get; set; }
}

public class AddCommentInput
{
    public string Comment { get; set; } = string.Empty;
}
