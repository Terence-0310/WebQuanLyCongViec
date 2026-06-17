using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
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

    /// <summary>Có nhiều hơn một người để chọn (SuperAdmin/Admin/Manager) -> hiển thị bộ chọn.</summary>
    public bool CanViewOthers { get; set; }
}

/// <summary>Trang quản lý người dùng: danh sách trong phạm vi quản lý của người xem.</summary>
public class UserIndexViewModel
{
    public List<UserListItemViewModel> Users { get; set; } = new();

    /// <summary>Nhân viên thuộc công ty (nằm trong phân cấp).</summary>
    public List<UserListItemViewModel> CompanyUsers =>
        Users.Where(u => u.AccountType == Cetee.Models.AccountType.Company).ToList();

    /// <summary>Tài khoản cá nhân (tự đăng ký / Google).</summary>
    public List<UserListItemViewModel> PersonalUsers =>
        Users.Where(u => u.AccountType == Cetee.Models.AccountType.Personal).ToList();

    /// <summary>Vai trò của người đang xem (quyết định nút "Tạo", cột hiển thị...).</summary>
    public string ViewerRole { get; set; } = Roles.User;

    /// <summary>Tổng số Admin trong hệ thống — chỉ SuperAdmin được biết (null với vai trò khác).</summary>
    public int? TotalAdmins { get; set; }

    /// <summary>Người xem có quyền tạo người dùng mới hay không (có vai trò thấp hơn để gán).</summary>
    public bool CanCreate { get; set; }
}

/// <summary>Form tạo tài khoản mới và chọn vai trò (trong phạm vi cho phép của người tạo).</summary>
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
    public string Role { get; set; } = string.Empty;

    /// <summary>Các vai trò người tạo được phép gán (luôn thấp hơn cấp của họ).</summary>
    public List<SelectListItem> RoleOptions { get; set; } = new();
}

/// <summary>Form sửa thông tin user và (tùy chọn) đặt lại mật khẩu.</summary>
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
    public string Role { get; set; } = string.Empty;

    // Để trống nếu không đổi mật khẩu. Độ dài tối thiểu kiểm tra ở controller
    // (chỉ khi có nhập) để tránh báo lỗi khi bỏ trống.
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string? NewPassword { get; set; }

    /// <summary>Các vai trò người sửa được phép gán (luôn thấp hơn cấp của họ).</summary>
    public List<SelectListItem> RoleOptions { get; set; } = new();

    /// <summary>True nếu đang sửa chính tài khoản đăng nhập (khóa đổi vai trò).</summary>
    public bool IsSelf { get; set; }
}

/// <summary>Một dòng trong bảng quản lý người dùng.</summary>
public class UserListItemViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;

    /// <summary>Loại tài khoản: cá nhân hay nhân viên công ty.</summary>
    public Cetee.Models.AccountType AccountType { get; set; }

    /// <summary>Tên cấp trên trực tiếp (null nếu không trực thuộc ai).</summary>
    public string? ManagerName { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Số workspace mà user này đang sở hữu (sẽ bị xóa kèm nếu xóa user).</summary>
    public int OwnedWorkspaceCount { get; set; }

    /// <summary>Số task đang được giao cho user (sẽ chuyển về chưa giao nếu xóa user).</summary>
    public int AssignedTaskCount { get; set; }

    /// <summary>True nếu là chính tài khoản đang đăng nhập.</summary>
    public bool IsSelf { get; set; }

    // --- Quyền thao tác của người xem trên dòng này (tính sẵn ở service) ---

    /// <summary>Người xem được sửa người này.</summary>
    public bool CanEdit { get; set; }

    /// <summary>Người xem được xóa người này (SuperAdmin không bao giờ bị xóa).</summary>
    public bool CanDelete { get; set; }

    /// <summary>Người xem được cấp/bỏ quyền Admin cho người này (chỉ SuperAdmin).</summary>
    public bool CanToggleAdmin { get; set; }

    public bool IsAdmin => RoleName == Roles.Admin;
    public bool IsSuperAdmin => RoleName == Roles.SuperAdmin;

    /// <summary>Nhãn tiếng Việt của vai trò.</summary>
    public string RoleLabel => Roles.Label(RoleName);
}
