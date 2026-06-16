using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cetee.Services;
using Cetee.ViewModels;

namespace Cetee.Controllers;

[Authorize]
public class WorkspacesController : BaseController
{
    private readonly IWorkspaceService _workspaces;

    public WorkspacesController(IWorkspaceService workspaces) => _workspaces = workspaces;

    // GET /Workspaces
    public async Task<IActionResult> Index()
    {
        var list = await _workspaces.GetForUserAsync(CurrentUserId, IsAdmin);
        return View(list);
    }

    [HttpGet]
    public IActionResult Create() => View(new WorkspaceFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WorkspaceFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        await _workspaces.CreateAsync(model, CurrentUserId);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var ws = await _workspaces.GetByIdForUserAsync(id, CurrentUserId, IsAdmin);
        if (ws is null) return NotFound();

        return View(new WorkspaceFormViewModel { Id = ws.Id, Name = ws.Name, Description = ws.Description });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(WorkspaceFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (!await _workspaces.UpdateAsync(model, CurrentUserId, IsAdmin)) return NotFound();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _workspaces.DeleteAsync(id, CurrentUserId, IsAdmin);
        return RedirectToAction(nameof(Index));
    }
}
