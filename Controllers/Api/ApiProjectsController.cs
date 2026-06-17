using Microsoft.AspNetCore.Mvc;
using Cetee.Services;
using Cetee.ViewModels;

namespace Cetee.Controllers.Api;

[Route("api/projects")]
public class ApiProjectsController : ApiBaseController
{
    private readonly IProjectService _projects;

    public ApiProjectsController(IProjectService projects)
    {
        _projects = projects;
    }

    [HttpGet]
    public async Task<IActionResult> GetProjects()
    {
        var projects = await _projects.GetForUserAsync(CurrentUserId, IsAdmin);
        var result = projects.Select(p => new
        {
            p.Id,
            p.Name,
            p.Description,
            p.WorkspaceId,
            workspaceName = p.Workspace.Name,
            taskCount = p.Tasks.Count,
            completedTaskCount = p.Tasks.Count(t => t.Status == Models.TaskStatus.Done),
            p.CreatedAt
        });

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDetails(int id)
    {
        var details = await _projects.GetDetailsAsync(id, CurrentUserId, IsAdmin);
        if (details is null)
            return NotFound(new { message = "Không tìm thấy dự án hoặc bạn không có quyền truy cập." });

        return Ok(new
        {
            project = new
            {
                details.Project.Id,
                details.Project.Name,
                details.Project.Description,
                details.Project.WorkspaceId,
                workspaceName = details.Project.Workspace.Name,
                details.Project.CreatedAt
            },
            totalTasks = details.TotalTasks,
            completedTasks = details.CompletedTasks,
            pages = details.Pages.Select(p => new
            {
                p.Id,
                p.Title,
                p.UpdatedAt
            }),
            tasks = details.Tasks.Select(t => new
            {
                t.Id,
                t.Title,
                t.Priority,
                status = t.Status.ToString(),
                t.DueDate,
                assigneeName = t.Assignee?.FullName
            }),
            members = details.Members.Select(m => new
            {
                m.UserId,
                userName = m.User.FullName,
                role = m.Role.ToString()
            }),
            recentActivities = details.RecentActivities.Select(a => new
            {
                a.Id,
                userName = a.User.FullName,
                action = a.Action,
                description = a.Description,
                a.CreatedAt
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProjectFormViewModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var project = await _projects.CreateAsync(model, CurrentUserId, IsAdmin);
        if (project is null)
            return BadRequest(new { message = "Không có quyền tạo dự án trong workspace này." });

        return CreatedAtAction(nameof(GetDetails), new { id = project.Id }, new { message = "Tạo dự án thành công.", id = project.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ProjectFormViewModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        model.Id = id;
        var success = await _projects.UpdateAsync(model, CurrentUserId, IsAdmin);
        if (!success)
            return NotFound(new { message = "Không tìm thấy dự án hoặc bạn không có quyền cập nhật." });

        return Ok(new { message = "Cập nhật dự án thành công." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _projects.DeleteAsync(id, CurrentUserId, IsAdmin);
        if (!success)
            return NotFound(new { message = "Không tìm thấy dự án hoặc bạn không có quyền xóa." });

        return Ok(new { message = "Xóa dự án thành công." });
    }
}
