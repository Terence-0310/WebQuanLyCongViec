using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cetee.Services;

namespace Cetee.Controllers;

[Authorize]
public class DashboardController : BaseController
{
    private readonly IDashboardService _dashboard;
    private readonly IUserService _users;

    public DashboardController(IDashboardService dashboard, IUserService users)
    {
        _dashboard = dashboard;
        _users = users;
    }

    public async Task<IActionResult> Index(int? employeeId)
    {
        var scope = await _users.ResolveScopeAsync(CurrentUserId, CurrentRole, employeeId);
        int? targetEmployee = scope.SelectedId == 0 ? null : scope.SelectedId;
        var model = await _dashboard.GetForUserAsync(CurrentUserId, IsAdmin, targetEmployee);
        model.Scope = scope;
        return View(model);
    }
}
