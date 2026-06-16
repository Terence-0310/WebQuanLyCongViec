using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cetee.Services;

namespace Cetee.Controllers;

[Authorize]
public class DashboardController : BaseController
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    public async Task<IActionResult> Index()
    {
        var model = await _dashboard.GetForUserAsync(CurrentUserId, IsAdmin);
        return View(model);
    }
}
