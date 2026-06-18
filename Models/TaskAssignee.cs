namespace Cetee.Models;

/// <summary>
/// Bảng nối nhiều-nhiều giữa <see cref="TaskItem"/> và <see cref="User"/>:
/// một task có thể giao cho nhiều người (đa phụ trách), một người nhận nhiều task.
/// Khóa chính kép (TaskItemId, UserId) để mỗi người chỉ xuất hiện một lần trong task.
/// </summary>
public class TaskAssignee
{
    public int TaskItemId { get; set; }
    public TaskItem TaskItem { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
