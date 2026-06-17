using System.ComponentModel.DataAnnotations;

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
}
