namespace Cetee.Models;

/// <summary>Bảng liên kết User - Project (quan hệ nhiều-nhiều có thêm vai trò).</summary>
public class ProjectMember
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public MemberRole Role { get; set; } = MemberRole.Member;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
