using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Cetee.Models;
using Cetee.Services;
using Cetee.ViewModels;

namespace Cetee.Controllers;

/// <summary>
/// Quản lý người dùng theo phân cấp công ty. SuperAdmin/Admin/Manager đều truy cập
/// được nhưng mỗi người chỉ thấy và thao tác trong "đội" của mình (xem <see cref="UserService"/>).
/// </summary>
[Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin},{Roles.Manager}")]
public class UsersController : BaseController
{
    private readonly IUserService _users;

    public UsersController(IUserService users) => _users = users;

    // GET /Users
    public async Task<IActionResult> Index()
    {
        var model = await _users.GetIndexAsync(CurrentUserId, CurrentRole);
        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!Roles.AssignableBy(CurrentRole).Any()) return Forbid();
        return View(new CreateUserViewModel { RoleOptions = AssignableRoleOptions(null) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.RoleOptions = AssignableRoleOptions(model.Role);
            return View(model);
        }

        var (ok, error) = await _users.CreateAsync(model, CurrentUserId, CurrentRole);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error!);
            model.RoleOptions = AssignableRoleOptions(model.Role);
            return View(model);
        }

        TempData["Success"] = "Đã tạo người dùng mới.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var vm = await _users.GetForEditAsync(id, CurrentUserId, CurrentRole);
        if (vm is null) return NotFound();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditUserViewModel model)
    {
        // Mật khẩu chỉ bắt buộc tối thiểu 6 ký tự khi có nhập (để trống = giữ nguyên).
        if (!string.IsNullOrEmpty(model.NewPassword) && model.NewPassword.Length < 6)
            ModelState.AddModelError(nameof(model.NewPassword), "Mật khẩu tối thiểu 6 ký tự.");

        model.IsSelf = model.Id == CurrentUserId;
        if (!ModelState.IsValid)
        {
            model.RoleOptions = AssignableRoleOptions(model.Role);
            return View(model);
        }

        var (ok, error) = await _users.UpdateAsync(model, CurrentUserId, CurrentRole);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error!);
            model.RoleOptions = AssignableRoleOptions(model.Role);
            return View(model);
        }

        TempData["Success"] = "Đã cập nhật người dùng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(int id, string role)
    {
        var (ok, error) = await _users.SetRoleAsync(id, role, CurrentUserId, CurrentRole);
        if (ok)
            TempData["Success"] = role == Roles.Admin ? "Đã cấp quyền Admin." : "Đã chuyển về vai trò User.";
        else
            TempData["Error"] = error;

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, error) = await _users.DeleteAsync(id, CurrentUserId, CurrentRole);
        if (ok)
            TempData["Success"] = "Đã xóa người dùng.";
        else
            TempData["Error"] = error;

        return RedirectToAction(nameof(Index));
    }

    // Các vai trò người đang đăng nhập được phép gán (luôn thấp hơn cấp của họ).
    private List<SelectListItem> AssignableRoleOptions(string? selected) =>
        Roles.AssignableBy(CurrentRole)
            .Select(r => new SelectListItem(Roles.Label(r), r, r == selected))
            .ToList();
}
