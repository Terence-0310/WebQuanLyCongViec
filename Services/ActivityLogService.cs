using Microsoft.EntityFrameworkCore;
using Cetee.Data;
using Cetee.Models;

namespace Cetee.Services;

public interface IActivityLogService
{
    Task LogAsync(int userId, string action, string entityType, int entityId, string description);

    /// <summary>Hoạt động gần đây cho Dashboard. Admin xem toàn bộ, user xem hành động của mình.</summary>
    Task<List<ActivityLog>> GetRecentAsync(int userId, bool isAdmin, int count = 8);

    /// <summary>Hoạt động liên quan tới một danh sách đối tượng (dùng cho trang chi tiết).</summary>
    Task<List<ActivityLog>> GetForEntitiesAsync(IEnumerable<(string Type, int Id)> entities, int count = 8);
}

/// <summary>Ghi và truy vấn nhật ký hoạt động.</summary>
public class ActivityLogService : IActivityLogService
{
    private readonly AppDbContext _db;

    public ActivityLogService(AppDbContext db) => _db = db;

    public async Task LogAsync(int userId, string action, string entityType, int entityId, string description)
    {
        _db.ActivityLogs.Add(new ActivityLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Description = description
        });
        await _db.SaveChangesAsync();
    }

    public Task<List<ActivityLog>> GetRecentAsync(int userId, bool isAdmin, int count = 8)
    {
        var query = _db.ActivityLogs.Include(a => a.User).AsQueryable();
        if (!isAdmin)
            query = query.Where(a => a.UserId == userId);

        return query.OrderByDescending(a => a.CreatedAt).Take(count).ToListAsync();
    }

    public async Task<List<ActivityLog>> GetForEntitiesAsync(IEnumerable<(string Type, int Id)> entities, int count = 8)
    {
        // Gom nhóm theo loại để tạo bộ lọc đơn giản, tránh OR phức tạp.
        var byType = entities
            .GroupBy(e => e.Type)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var result = new List<ActivityLog>();
        foreach (var (type, ids) in byType)
        {
            var logs = await _db.ActivityLogs
                .Include(a => a.User)
                .Where(a => a.EntityType == type && ids.Contains(a.EntityId))
                .ToListAsync();
            result.AddRange(logs);
        }

        return result.OrderByDescending(a => a.CreatedAt).Take(count).ToList();
    }
}
