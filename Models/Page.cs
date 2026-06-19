using System.ComponentModel.DataAnnotations;

namespace Cetee.Models;

/// <summary>Trang ghi chú thuộc một project. Nội dung là text/rich text đơn giản.</summary>
public class Page
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    public string? Content { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
