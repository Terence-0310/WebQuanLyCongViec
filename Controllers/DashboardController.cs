using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cetee.Models;
using Cetee.Services;

namespace Cetee.Controllers;

/// <summary>Trang tổng quan — chỉ SuperAdmin và Admin được xem.</summary>
[Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
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

        // Xác định tập người dùng để tổng hợp số liệu:
        //  - Đã chọn một người cụ thể -> chỉ người đó.
        //  - "Tất cả" + SuperAdmin    -> toàn hệ thống (null = không lọc).
        //  - "Tất cả" + Admin         -> cả đội của mình.
        List<int>? scopeUserIds;
        if (scope.SelectedId != 0)
            scopeUserIds = new List<int> { scope.SelectedId };
        else if (CanSeeAllData)
            scopeUserIds = null;
        else
            scopeUserIds = scope.Visible.Select(u => u.Id).ToList();

        var model = await _dashboard.GetForScopeAsync(scopeUserIds);
        model.Scope = scope;
        return View(model);
    }
}
