using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cetee.Models;
using Cetee.Services;
using Cetee.ViewModels;

namespace Cetee.Controllers;

[Authorize]
public class WorkspacesController : BaseController
{
    private readonly IWorkspaceService _workspaces;
    private readonly IUserService _users;

    public WorkspacesController(IWorkspaceService workspaces, IUserService users)
    {
        _workspaces = workspaces;
        _users = users;
    }

    // GET /Workspaces
    public async Task<IActionResult> Index()
    {
        var model = await _workspaces.GetIndexAsync(CurrentUserId, CanSeeAllData);
        return View(model);
    }

    // GET /Workspaces/Details/{id}
    public async Task<IActionResult> Details(int id)
    {
        var model = await _workspaces.GetDetailsAsync(id, CurrentUserId, CurrentRole);
        if (model is null) return NotFound();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new WorkspaceFormViewModel
        {
            CandidateUsers = await CandidatesAsync(),
            IsPersonalCreator = IsPersonalAccount
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WorkspaceFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.CandidateUsers = await CandidatesAsync();
            model.IsPersonalCreator = IsPersonalAccount;
            return View(model);
        }

        var ws = await _workspaces.CreateAsync(model, CurrentUserId, CurrentRole);
        return RedirectToAction(nameof(Details), new { id = ws.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var ws = await _workspaces.GetByIdForUserAsync(id, CurrentUserId, CanSeeAllData);
        if (ws is null) return NotFound();

        return View(new WorkspaceFormViewModel { Id = ws.Id, Name = ws.Name, Description = ws.Description });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(WorkspaceFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (!await _workspaces.UpdateAsync(model, CurrentUserId, CanSeeAllData)) return NotFound();
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _workspaces.DeleteAsync(id, CurrentUserId, CanSeeAllData);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(int workspaceId, int userId)
    {
        if (!await _workspaces.AddMemberAsync(workspaceId, userId, CurrentUserId, CurrentRole))
            TempData["Error"] = "Không thể thêm thành viên (ngoài phạm vi quản lý hoặc không có quyền).";
        else
            TempData["Success"] = "Đã thêm thành viên vào workspace.";
        return RedirectToAction(nameof(Details), new { id = workspaceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(int workspaceId, int userId)
    {
        if (!await _workspaces.RemoveMemberAsync(workspaceId, userId, CurrentUserId, CurrentRole))
            TempData["Error"] = "Không thể gỡ thành viên này.";
        else
            TempData["Success"] = "Đã gỡ thành viên khỏi workspace.";
        return RedirectToAction(nameof(Details), new { id = workspaceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetMemberRole(int workspaceId, int userId, MemberRole role)
    {
        if (!await _workspaces.SetMemberRoleAsync(workspaceId, userId, role, CurrentUserId, CurrentRole))
            TempData["Error"] = "Không thể đổi vai trò thành viên này.";
        else
            TempData["Success"] = "Đã cập nhật vai trò thành viên.";
        return RedirectToAction(nameof(Details), new { id = workspaceId });
    }

    // Ứng viên thêm vào workspace = những người trong phạm vi quản lý, trừ chính mình (đã là chủ).
    private async Task<List<Cetee.Models.User>> CandidatesAsync()
    {
        var visible = await _users.GetVisibleEmployeesAsync(CurrentUserId, CurrentRole);
        return visible.Where(u => u.Id != CurrentUserId).ToList();
    }
}
