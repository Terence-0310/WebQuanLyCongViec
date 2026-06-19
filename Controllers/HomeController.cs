using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Cetee.Models;

namespace Cetee.Controllers;

public class HomeController : Controller
{
    // Trang gốc: đã đăng nhập -> Dashboard, chưa đăng nhập -> Login.
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        return RedirectToAction("Login", "Account");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
