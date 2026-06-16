using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cetee.Services;
using Cetee.ViewModels;

namespace Cetee.Controllers;

[Authorize]
public class PagesController : BaseController
{
    private readonly IPageService _pages;

    public PagesController(IPageService pages) => _pages = pages;

    // GET /Pages/Details/{id}
    public async Task<IActionResult> Details(int id)
    {
        var page = await _pages.GetByIdForUserAsync(id, CurrentUserId, IsAdmin);
        if (page is null) return NotFound();
        return View(page);
    }

    [HttpGet]
    public IActionResult Create(int projectId) =>
        View(new PageFormViewModel { ProjectId = projectId });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PageFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var page = await _pages.CreateAsync(model, CurrentUserId, IsAdmin);
        if (page is null) return NotFound();

        return RedirectToAction(nameof(Details), new { id = page.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var page = await _pages.GetByIdForUserAsync(id, CurrentUserId, IsAdmin);
        if (page is null) return NotFound();

        return View(new PageFormViewModel
        {
            Id = page.Id,
            Title = page.Title,
            Content = page.Content,
            ProjectId = page.ProjectId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PageFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (!await _pages.UpdateAsync(model, CurrentUserId, IsAdmin)) return NotFound();
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var projectId = await _pages.DeleteAsync(id, CurrentUserId, IsAdmin);
        if (projectId is null) return NotFound();
        return RedirectToAction("Details", "Projects", new { id = projectId.Value });
    }
}
