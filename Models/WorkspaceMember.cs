namespace Cetee.Models;

/// <summary>Bảng liên kết User - Workspace (quan hệ nhiều-nhiều có thêm vai trò).</summary>
public class WorkspaceMember
{
    public int Id { get; set; }

    public int WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public MemberRole Role { get; set; } = MemberRole.Member;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
