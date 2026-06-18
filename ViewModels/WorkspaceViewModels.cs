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

    /// <summary>Người tạo là tài khoản cá nhân (không có đội ngũ) — hiển thị giao diện tối giản.</summary>
    public bool IsPersonalCreator { get; set; }
}

/// <summary>Một thẻ workspace trên trang danh sách, kèm bối cảnh của người đang xem.</summary>
public class WorkspaceCardViewModel
{
    public Workspace Workspace { get; set; } = null!;

    /// <summary>Vai trò của người xem trong workspace này.</summary>
    public MemberRole MyRole { get; set; }

    /// <summary>Người xem có phải chủ sở hữu workspace không.</summary>
    public bool IsOwner { get; set; }

    /// <summary>Tổng số task trong tất cả project của workspace.</summary>
    public int TaskCount { get; set; }

    /// <summary>Không gian cá nhân: chỉ có một thành viên (chủ sở hữu).</summary>
    public bool IsPersonal { get; set; }
}

/// <summary>Trang danh sách workspace: tách "của tôi" và "tôi tham gia".</summary>
public class WorkspaceIndexViewModel
{
    public List<WorkspaceCardViewModel> Owned { get; set; } = new();
    public List<WorkspaceCardViewModel> Joined { get; set; } = new();

    /// <summary>Người xem là tài khoản cá nhân — dùng để điều chỉnh ngôn ngữ giao diện.</summary>
    public bool ViewerIsPersonal { get; set; }

    public bool HasAny => Owned.Count > 0 || Joined.Count > 0;
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

    /// <summary>Không gian cá nhân: chỉ có chủ sở hữu, không có ai khác.</summary>
    public bool IsPersonal { get; set; }

    /// <summary>Vai trò của người xem trong workspace.</summary>
    public MemberRole MyRole { get; set; }

    public int TaskCount => Projects.Sum(p => p.Tasks.Count);
}
