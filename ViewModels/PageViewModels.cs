using System.ComponentModel.DataAnnotations;

namespace Cetee.ViewModels;

public class PageFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề.")]
    [StringLength(150)]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Nội dung")]
    public string? Content { get; set; }

    [Required]
    [Display(Name = "Project")]
    public int ProjectId { get; set; }
}
