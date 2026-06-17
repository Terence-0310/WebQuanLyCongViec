using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Cetee.Models;

/// <summary>
/// Tài khoản người dùng — kế thừa <see cref="IdentityUser{TKey}"/> với khóa kiểu int
/// (giữ nguyên mọi khóa ngoại int trong hệ thống). Identity cung cấp Id, Email,
/// UserName, PasswordHash...; lớp này bổ sung các trường nghiệp vụ riêng.
/// </summary>
public class User : IdentityUser<int>
{
    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    // Một user có đúng một vai trò (FK trực tiếp, song song với Identity roles).
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    /// <summary>Phân loại tài khoản: cá nhân hay nhân viên công ty.</summary>
    public AccountType AccountType { get; set; } = AccountType.Personal;

    // Người quản lý trực tiếp (tự tham chiếu User). Null = chủ hệ thống hoặc cá nhân độc lập.
    public int? ManagerId { get; set; }
    public User? Manager { get; set; }
    public ICollection<User> Subordinates { get; set; } = new List<User>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<WorkspaceMember> WorkspaceMemberships { get; set; } = new List<WorkspaceMember>();
    public ICollection<ProjectMember> ProjectMemberships { get; set; } = new List<ProjectMember>();
    public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
