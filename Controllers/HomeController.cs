using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Cetee.Models;

namespace Cetee.Controllers;

public class HomeController : Controller
{
    // Trang gốc:
    //  - Chưa đăng nhập            -> Landing page giới thiệu sản phẩm (cho khách xem trước).
    //  - SuperAdmin / Admin        -> Dashboard (chỉ hai vai trò này được xem).
    //  - Manager / User (& độc lập) -> Workspaces (khu làm việc của họ).
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true)
            return View(); // Landing page công khai.

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
