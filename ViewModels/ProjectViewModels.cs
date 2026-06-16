using System.ComponentModel.DataAnnotations;

namespace Cetee.ViewModels;

public class ProjectFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên project.")]
    [StringLength(120)]
    [Display(Name = "Tên project")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn workspace.")]
    [Display(Name = "Workspace")]
    public int WorkspaceId { get; set; }
}
