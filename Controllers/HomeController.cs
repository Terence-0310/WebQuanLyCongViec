using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Cetee.Models;

namespace Cetee.Controllers;

public class HomeController : Controller
{
    // Trang gốc: điều hướng theo trạng thái và vai trò.
    //  - Chưa đăng nhập            -> Login.
    //  - SuperAdmin / Admin        -> Dashboard (chỉ hai vai trò này được xem).
    //  - Manager / User (& độc lập) -> Workspaces (khu làm việc của họ).
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

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
