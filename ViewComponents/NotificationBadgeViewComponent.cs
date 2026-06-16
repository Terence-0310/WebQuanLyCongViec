using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Cetee.Services;

namespace Cetee.ViewComponents;

/// <summary>Hiển thị số thông báo chưa đọc cạnh menu Notifications trên sidebar.</summary>
public class NotificationBadgeViewComponent : ViewComponent
{
    private readonly INotificationService _notifications;

    public NotificationBadgeViewComponent(INotificationService notifications) => _notifications = notifications;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var idClaim = (User as ClaimsPrincipal)?.FindFirst(ClaimTypes.NameIdentifier);
        if (idClaim is null) return Content(string.Empty);

        int count = await _notifications.GetUnreadCountAsync(int.Parse(idClaim.Value));
        return View(count);
    }
}
