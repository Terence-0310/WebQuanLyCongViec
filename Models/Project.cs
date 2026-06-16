using System.ComponentModel.DataAnnotations;

namespace Cetee.Models;

/// <summary>Dự án thuộc một workspace, chứa các page ghi chú và task.</summary>
public class Project
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public int WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public ICollection<Page> Pages { get; set; } = new List<Page>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
