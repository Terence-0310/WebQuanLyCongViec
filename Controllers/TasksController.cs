using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Cetee.Services;
using Cetee.ViewModels;
using TaskStatus = Cetee.Models.TaskStatus;

namespace Cetee.Controllers;

[Authorize]
public class TasksController : BaseController
{
    private readonly ITaskService _tasks;
    private readonly IUserService _users;

    public TasksController(ITaskService tasks, IUserService users)
    {
        _tasks = tasks;
        _users = users;
    }

    // GET /Tasks  -> view dạng List, có lọc theo project, status và người thực hiện
    public async Task<IActionResult> Index(int? projectId, TaskStatus? status, string? q, int? employeeId)
    {
        var scope = await _users.ResolveScopeAsync(CurrentUserId, CurrentRole, employeeId);
        var tasks = await _tasks.GetForUserAsync(CurrentUserId, CanSeeAllData, projectId, status, q, scope.AssigneeFilter);
        var model = new TaskListViewModel
        {
            Tasks = tasks,
            ProjectId = projectId,
            Status = status,
            Search = q,
            ProjectOptions = await ProjectOptionsAsync(projectId),
            Scope = scope
        };
        return View(model);
    }

    // GET /Tasks/Board -> view dạng Kanban 3 cột
    public async Task<IActionResult> Board(int? projectId, int? employeeId)
    {
        var scope = await _users.ResolveScopeAsync(CurrentUserId, CurrentRole, employeeId);
        var tasks = await _tasks.GetForUserAsync(CurrentUserId, CanSeeAllData, projectId, null, null, scope.AssigneeFilter);
        var model = new KanbanViewModel
        {
            ProjectId = projectId,
            ProjectOptions = await ProjectOptionsAsync(projectId),
            Todo = tasks.Where(t => t.Status == TaskStatus.Todo).ToList(),
            Doing = tasks.Where(t => t.Status == TaskStatus.Doing).ToList(),
            Done = tasks.Where(t => t.Status == TaskStatus.Done).ToList(),
            Scope = scope
        };
        return View(model);
    }

    // GET /Tasks/Timeline -> lịch ngày kéo thả (mặc định hôm nay)
    public async Task<IActionResult> Timeline(DateTime? date, int? employeeId)
    {
        var day = (date ?? DateTime.Today).Date;
        var scope = await _users.ResolveScopeAsync(CurrentUserId, CurrentRole, employeeId);
        var model = await _tasks.GetTimelineAsync(CurrentUserId, CanSeeAllData, day, scope.AssigneeFilter);
        model.Scope = scope;
        return View(model);
    }

    // POST /Tasks/Schedule -> xếp/đổi/bỏ lịch hoặc đổi thời lượng (gọi bằng AJAX từ timeline)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Schedule(int id, string? start, int? duration)
    {
        // start không gửi -> giữ nguyên giờ; start "" -> bỏ lịch; ngược lại "yyyy-MM-ddTHH:mm".
        bool changeStart = start != null;
        DateTime? when = null;
        if (changeStart && start!.Length > 0)
        {
            if (!DateTime.TryParse(start, out var parsed))
                return BadRequest(new { ok = false, message = "Thời gian không hợp lệ." });
            when = parsed;
        }

        var ok = await _tasks.ScheduleAsync(id, changeStart, when, duration, CurrentUserId, CanSeeAllData);
        if (!ok) return NotFound(new { ok = false });
        return Json(new { ok = true });
    }

    // GET /Tasks/Details/{id}
    public async Task<IActionResult> Details(int id)
    {
        var model = await _tasks.GetDetailsAsync(id, CurrentUserId, CanSeeAllData);
        if (model is null) return NotFound();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? projectId)
    {
        var model = new TaskFormViewModel { ProjectId = projectId ?? 0 };
        await PopulateOptions(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TaskFormViewModel model)
    {
        // Khi tạo mới, deadline không được nằm trong quá khứ.
        if (model.DueDate.HasValue && model.DueDate.Value.Date < DateTime.UtcNow.Date)
            ModelState.AddModelError(nameof(model.DueDate), "Hạn chót không được nhỏ hơn ngày hiện tại.");

        if (!ModelState.IsValid)
        {
            await PopulateOptions(model);
            return View(model);
        }

        var task = await _tasks.CreateAsync(model, CurrentUserId, CanSeeAllData);
        if (task is null)
        {
            ModelState.AddModelError(string.Empty, "Bạn không có quyền tạo task trong project này.");
            await PopulateOptions(model);
            return View(model);
        }

        return RedirectToAction(nameof(Details), new { id = task.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var task = await _tasks.GetByIdForUserAsync(id, CurrentUserId, CanSeeAllData);
        if (task is null) return NotFound();

        var model = new TaskFormViewModel
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            ProjectId = task.ProjectId,
            AssigneeId = task.AssigneeId,
            Priority = task.Priority,
            Status = task.Status,
            DueDate = task.DueDate,
            DurationMinutes = task.DurationMinutes
        };
        await PopulateOptions(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(TaskFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateOptions(model);
            return View(model);
        }

        if (!await _tasks.UpdateAsync(model, CurrentUserId, CanSeeAllData)) return NotFound();
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _tasks.DeleteAsync(id, CurrentUserId, CanSeeAllData);
        return RedirectToAction(nameof(Index));
    }

    // Đổi nhanh trạng thái từ bảng Kanban hoặc list.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, TaskStatus status, string? returnUrl)
    {
        await _tasks.ChangeStatusAsync(id, status, CurrentUserId, CanSeeAllData);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Board));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int id, string newComment)
    {
        if (!string.IsNullOrWhiteSpace(newComment))
            await _tasks.AddCommentAsync(id, CurrentUserId, CanSeeAllData, newComment);

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<List<SelectListItem>> ProjectOptionsAsync(int? selected)
    {
        var projects = await _tasks.GetAccessibleProjectsAsync(CurrentUserId, CanSeeAllData);
        return projects
            .Select(p => new SelectListItem(p.Name, p.Id.ToString(), p.Id == selected))
            .ToList();
    }

    // Đổ dropdown project và người thực hiện cho form task.
    private async Task PopulateOptions(TaskFormViewModel model)
    {
        model.ProjectOptions = await ProjectOptionsAsync(model.ProjectId);

        if (model.ProjectId > 0)
        {
            var members = await _tasks.GetProjectMembersAsync(model.ProjectId);
            model.AssigneeOptions = members
                .Select(u => new SelectListItem(u.FullName, u.Id.ToString(), u.Id == model.AssigneeId))
                .ToList();
        }
    }
}
