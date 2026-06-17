using Microsoft.EntityFrameworkCore;
using Cetee.Data;
using Cetee.Models;

namespace Cetee.Services;

public interface INotificationService
{
    Task CreateAsync(int userId, string message, int? taskItemId = null);
    Task<List<Notification>> GetForUserAsync(int userId);
    Task<int> GetUnreadCountAsync(int userId);
    Task<bool> MarkAsReadAsync(int id, int userId);
    Task MarkAllAsReadAsync(int userId);
}

/// <summary>Nghiệp vụ tạo và quản lý thông báo cho người dùng.</summary>
public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db) => _db = db;

    public async Task CreateAsync(int userId, string message, int? taskItemId = null)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Message = message,
            TaskItemId = taskItemId
        });
        await _db.SaveChangesAsync();
    }

    public Task<List<Notification>> GetForUserAsync(int userId) =>
        _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

    public Task<int> GetUnreadCountAsync(int userId) =>
        _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

    public async Task<bool> MarkAsReadAsync(int id, int userId)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (n is null) return false;
        n.IsRead = true;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }
}
