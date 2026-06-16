using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cetee.Services;
using Cetee.ViewModels;

namespace Cetee.Controllers;

/// <summary>Quản lý người dùng và phân quyền — chỉ Admin được truy cập.</summary>
[Authorize(Roles = "Admin")]
public class UsersController : BaseController
{
    private readonly IUserService _users;

    public UsersController(IUserService users) => _users = users;

    // GET /Users
    public async Task<IActionResult> Index()
    {
        var list = await _users.GetAllAsync(CurrentUserId);
        return View(list);
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateUserViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var (ok, error) = await _users.CreateAsync(model);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error!);
            return View(model);
        }

        TempData["Success"] = "Đã tạo người dùng mới.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var vm = await _users.GetForEditAsync(id, CurrentUserId);
        if (vm is null) return NotFound();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditUserViewModel model)
    {
        // Mật khẩu chỉ bắt buộc tối thiểu 6 ký tự khi Admin có nhập (để trống = giữ nguyên).
        if (!string.IsNullOrEmpty(model.NewPassword) && model.NewPassword.Length < 6)
            ModelState.AddModelError(nameof(model.NewPassword), "Mật khẩu tối thiểu 6 ký tự.");

        model.IsSelf = model.Id == CurrentUserId;
        if (!ModelState.IsValid) return View(model);

        var (ok, error) = await _users.UpdateAsync(model, CurrentUserId);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error!);
            return View(model);
        }

        TempData["Success"] = "Đã cập nhật người dùng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(int id, string role)
    {
        var (ok, error) = await _users.SetRoleAsync(id, role, CurrentUserId);
        if (ok)
            TempData["Success"] = role == "Admin" ? "Đã cấp quyền Admin." : "Đã chuyển về vai trò User.";
        else
            TempData["Error"] = error;

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, error) = await _users.DeleteAsync(id, CurrentUserId);
        if (ok)
            TempData["Success"] = "Đã xóa người dùng.";
        else
            TempData["Error"] = error;

        return RedirectToAction(nameof(Index));
    }
}
