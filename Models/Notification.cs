using System.ComponentModel.DataAnnotations;

namespace Cetee.Models;

/// <summary>Thông báo gửi tới một user (ví dụ khi được giao task).</summary>
public class Notification
{
    public int Id { get; set; }

    [Required, MaxLength(250)]
    public string Message { get; set; } = string.Empty;

    // Liên kết tới task liên quan (nếu có) để điều hướng nhanh.
    public int? TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
