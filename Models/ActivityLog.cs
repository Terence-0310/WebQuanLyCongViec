using System.ComponentModel.DataAnnotations;

namespace Cetee.Models;

/// <summary>Nhật ký hoạt động: ghi lại các hành động chính trong hệ thống.</summary>
public class ActivityLog
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Hành động ngắn gọn, ví dụ: "Created", "Updated", "ChangedStatus", "Commented".</summary>
    [Required, MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    /// <summary>Loại đối tượng bị tác động, ví dụ: "Project", "Task", "Comment".</summary>
    [Required, MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Khóa chính của đối tượng bị tác động.</summary>
    public int EntityId { get; set; }

    /// <summary>Mô tả dễ đọc để hiển thị trên giao diện.</summary>
    [Required, MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
