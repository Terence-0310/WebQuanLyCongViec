using System.ComponentModel.DataAnnotations;

namespace Cetee.Models;

/// <summary>Bình luận của user trong một task.</summary>
public class TaskComment
{
    public int Id { get; set; }

    [Required, MaxLength(1000)]
    public string Content { get; set; } = string.Empty;

    public int TaskItemId { get; set; }
    public TaskItem TaskItem { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
