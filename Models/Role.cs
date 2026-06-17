using Microsoft.AspNetCore.Identity;

namespace Cetee.Models;

/// <summary>Vai trò hệ thống phân cấp (SuperAdmin / Admin / Manager / User) — kế thừa
/// <see cref="IdentityRole{TKey}"/> khóa int. Xem <see cref="Roles"/>.</summary>
public class Role : IdentityRole<int>
{
    public Role() { }
    public Role(string name) : base(name) { }

    /// <summary>Các user gắn vai trò này qua FK trực tiếp <see cref="User.RoleId"/>.</summary>
    public ICollection<User> Users { get; set; } = new List<User>();
}
