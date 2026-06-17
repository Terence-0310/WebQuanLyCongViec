using System.ComponentModel.DataAnnotations;

namespace Cetee.Models;

/// <summary>Vai trò hệ thống (Admin / User) dùng cho phân quyền cơ bản.</summary>
public class Role
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    public ICollection<User> Users { get; set; } = new List<User>();
}
