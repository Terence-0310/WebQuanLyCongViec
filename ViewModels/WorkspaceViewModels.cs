using System.ComponentModel.DataAnnotations;
using Cetee.Models;

namespace Cetee.ViewModels;

public class WorkspaceFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên workspace.")]
    [StringLength(120)]
    [Display(Name = "Tên workspace")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    /// <summary>Các user được chọn để thêm vào workspace ngay khi tạo (ngoài chủ sở hữu).</summary>
    public List<int> MemberIds { get; set; } = new();

    /// <summary>Danh sách ứng viên để chọn (những người trong phạm vi quản lý của người tạo).</summary>
    public List<User> CandidateUsers { get; set; } = new();
}

/// <summary>Trang chi tiết workspace: thông tin, thành viên, project và panel quản lý thành viên.</summary>
public class WorkspaceDetailsViewModel
{
    public Workspace Workspace { get; set; } = null!;
    public List<WorkspaceMember> Members { get; set; } = new();
    public List<Project> Projects { get; set; } = new();

    /// <summary>Người được phép thêm vào (trong phạm vi quản lý, chưa là thành viên). Rỗng nếu không có quyền.</summary>
    public List<User> AddableUsers { get; set; } = new();

    /// <summary>Người xem có quyền thêm/bớt thành viên (chủ sở hữu hoặc SuperAdmin).</summary>
    public bool CanManageMembers { get; set; }
}
