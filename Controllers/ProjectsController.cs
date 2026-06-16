using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Cetee.Services;
using Cetee.ViewModels;

namespace Cetee.Controllers;

[Authorize]
public class ProjectsController : BaseController
{
    private readonly IProjectService _projects;
    private readonly IWorkspaceService _workspaces;

    public ProjectsController(IProjectService projects, IWorkspaceService workspaces)
    {
        _projects = projects;
        _workspaces = workspaces;
    }

    // GET /Projects
    public async Task<IActionResult> Index()
    {
        var list = await _projects.GetForUserAsync(CurrentUserId, IsAdmin);
        return View(list);
    }

    // GET /Projects/Details/{id}
    public async Task<IActionResult> Details(int id)
    {
        var model = await _projects.GetDetailsAsync(id, CurrentUserId, IsAdmin);
        if (model is null) return NotFound();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? workspaceId)
    {
        await PopulateWorkspaces();
        return View(new ProjectFormViewModel { WorkspaceId = workspaceId ?? 0 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProjectFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateWorkspaces();
            return View(model);
        }

        var project = await _projects.CreateAsync(model, CurrentUserId, IsAdmin);
        if (project is null)
        {
            ModelState.AddModelError(string.Empty, "Bạn không có quyền tạo project trong workspace này.");
            await PopulateWorkspaces();
            return View(model);
        }

        return RedirectToAction(nameof(Details), new { id = project.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var project = await _projects.GetByIdForUserAsync(id, CurrentUserId, IsAdmin);
        if (project is null) return NotFound();

        await PopulateWorkspaces();
        return View(new ProjectFormViewModel
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            WorkspaceId = project.WorkspaceId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProjectFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateWorkspaces();
            return View(model);
        }

        if (!await _projects.UpdateAsync(model, CurrentUserId, IsAdmin)) return NotFound();
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _projects.DeleteAsync(id, CurrentUserId, IsAdmin);
        return RedirectToAction(nameof(Index));
    }

    // Đổ danh sách workspace vào dropdown của form project.
    private async Task PopulateWorkspaces()
    {
        var workspaces = await _workspaces.GetForUserAsync(CurrentUserId, IsAdmin);
        ViewBag.Workspaces = workspaces
            .Select(w => new SelectListItem(w.Name, w.Id.ToString()))
            .ToList();
    }
}
