using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cetee.Services;

namespace Cetee.Controllers;

[Authorize]
public class NotificationsController : BaseController
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications) => _notifications = notifications;

    // GET /Notifications
    public async Task<IActionResult> Index()
    {
        var list = await _notifications.GetForUserAsync(CurrentUserId);
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        await _notifications.MarkAsReadAsync(id, CurrentUserId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        await _notifications.MarkAllAsReadAsync(CurrentUserId);
        return RedirectToAction(nameof(Index));
    }
}
