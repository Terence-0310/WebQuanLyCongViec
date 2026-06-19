using Microsoft.EntityFrameworkCore;
using Cetee.Models;

namespace Cetee.Data;

/// <summary>DbContext chính của ứng dụng (EF Core Code First).</summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Email là duy nhất.
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Một user chỉ tham gia một workspace một lần.
        modelBuilder.Entity<WorkspaceMember>()
            .HasIndex(m => new { m.WorkspaceId, m.UserId })
            .IsUnique();

        // Một user chỉ tham gia một project một lần.
        modelBuilder.Entity<ProjectMember>()
            .HasIndex(m => new { m.ProjectId, m.UserId })
            .IsUnique();

        // Enum lưu dưới dạng số (mặc định) - giữ rõ ràng và gọn.

        // Quan hệ Workspace -> Owner: không cascade để tránh multiple cascade paths.
        modelBuilder.Entity<Workspace>()
            .HasOne(w => w.Owner)
            .WithMany()
            .HasForeignKey(w => w.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Xóa workspace -> xóa các project con.
        modelBuilder.Entity<Project>()
            .HasOne(p => p.Workspace)
            .WithMany(w => w.Projects)
            .HasForeignKey(p => p.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Xóa project -> xóa page và task.
        modelBuilder.Entity<Page>()
            .HasOne(p => p.Project)
            .WithMany(pr => pr.Pages)
            .HasForeignKey(p => p.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TaskItem>()
            .HasOne(t => t.Project)
            .WithMany(p => p.Tasks)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Người được giao task: không cascade (xóa user không xóa task).
        modelBuilder.Entity<TaskItem>()
            .HasOne(t => t.Assignee)
            .WithMany(u => u.AssignedTasks)
            .HasForeignKey(t => t.AssigneeId)
            .OnDelete(DeleteBehavior.SetNull);

        // Comment thuộc task (cascade) và user (restrict để tránh vòng cascade).
        modelBuilder.Entity<TaskComment>()
            .HasOne(c => c.TaskItem)
            .WithMany(t => t.Comments)
            .HasForeignKey(c => c.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TaskComment>()
            .HasOne(c => c.User)
            .WithMany(u => u.Comments)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Notification thuộc user (cascade) và có thể trỏ tới task (restrict).
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.TaskItem)
            .WithMany()
            .HasForeignKey(n => n.TaskItemId)
            .OnDelete(DeleteBehavior.SetNull);

        // Membership: restrict trên user để tránh nhiều đường cascade về User.
        modelBuilder.Entity<WorkspaceMember>()
            .HasOne(m => m.User)
            .WithMany(u => u.WorkspaceMemberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WorkspaceMember>()
            .HasOne(m => m.Workspace)
            .WithMany(w => w.Members)
            .HasForeignKey(m => m.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProjectMember>()
            .HasOne(m => m.User)
            .WithMany(u => u.ProjectMemberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProjectMember>()
            .HasOne(m => m.Project)
            .WithMany(p => p.Members)
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ActivityLog>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
