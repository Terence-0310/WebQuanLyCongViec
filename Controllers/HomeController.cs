using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Cetee.Models;

namespace Cetee.Controllers;

public class HomeController : Controller
{
    // Trang chủ (landing) — luôn hiển thị cho cả khách lẫn người đã đăng nhập,
    // để bấm logo ở bất kỳ trang nào cũng quay về được trang chủ.
    public IActionResult Index() => View();

    // "Vào ứng dụng": điều hướng theo vai trò (dùng sau đăng nhập và cho nút trên landing).
    //  - SuperAdmin / Admin         -> Dashboard
    //  - Manager / User (& độc lập) -> Workspaces
    public IActionResult App()
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction(nameof(Index));

        var role = User.FindFirstValue(ClaimTypes.Role);
        return Roles.CanAccessDashboard(role)
            ? RedirectToAction("Index", "Dashboard")
            : RedirectToAction("Index", "Workspaces");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
