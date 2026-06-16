using System.ComponentModel.DataAnnotations;
using Cetee.Models;

namespace Cetee.ViewModels;

/// <summary>
/// Phạm vi "xem theo người" dùng chung cho Lịch ngày / Kanban / Danh sách / Dashboard.
/// Cho biết danh sách người được phép chọn, người đang chọn, và bộ lọc assignee tương ứng.
/// </summary>
public class EmployeeScopeResult
{
    /// <summary>Những người mà người xem được phép xem (gồm chính mình).</summary>
    public List<User> Visible { get; set; } = new();

    /// <summary>Id đang chọn. 0 = "Tất cả".</summary>
    public int SelectedId { get; set; }

    /// <summary>Bộ lọc theo assignee. null = không lọc (xem Tất cả trong phạm vi truy cập).</summary>
    public List<int>? AssigneeFilter { get; set; }

    /// <summary>Có nhiều hơn một người để chọn (Admin/Manager) -> hiển thị bộ chọn.</summary>
    public bool CanViewOthers { get; set; }
}

/// <summary>Form Admin tạo tài khoản mới và chọn vai trò.</summary>
public class CreateUserViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [Display(Name = "Họ và tên")]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    [Display(Name = "Vai trò")]
    public string Role { get; set; } = "User";

    [Display(Name = "Người quản lý")]
    public int? ManagerId { get; set; }

    /// <summary>Danh sách người có thể chọn làm quản lý (đổ dropdown).</summary>
    public List<User> ManagerOptions { get; set; } = new();
}

/// <summary>Form Admin sửa thông tin user và (tùy chọn) đặt lại mật khẩu.</summary>
public class EditUserViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [Display(Name = "Họ và tên")]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    [Display(Name = "Vai trò")]
    public string Role { get; set; } = "User";

    // Để trống nếu không đổi mật khẩu. Độ dài tối thiểu kiểm tra ở controller
    // (chỉ khi có nhập) để tránh báo lỗi khi bỏ trống.
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string? NewPassword { get; set; }

    [Display(Name = "Người quản lý")]
    public int? ManagerId { get; set; }

    /// <summary>Danh sách người có thể chọn làm quản lý (đổ dropdown).</summary>
    public List<User> ManagerOptions { get; set; } = new();

    /// <summary>True nếu đang sửa chính tài khoản đăng nhập (khóa đổi vai trò).</summary>
    public bool IsSelf { get; set; }
}

/// <summary>Một dòng trong bảng quản lý người dùng (dành cho Admin).</summary>
public class UserListItemViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;

    /// <summary>Tên người quản lý trực tiếp (null nếu không trực thuộc ai).</summary>
    public string? ManagerName { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Số workspace mà user này đang sở hữu (sẽ bị xóa kèm nếu xóa user).</summary>
    public int OwnedWorkspaceCount { get; set; }

    /// <summary>Số task đang được giao cho user (sẽ chuyển về chưa giao nếu xóa user).</summary>
    public int AssignedTaskCount { get; set; }

    /// <summary>True nếu là chính tài khoản đang đăng nhập (không cho tự đổi quyền/xóa).</summary>
    public bool IsSelf { get; set; }

    public bool IsAdmin => RoleName == "Admin";
}
