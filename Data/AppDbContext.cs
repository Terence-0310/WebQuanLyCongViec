using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Cetee.Models;

namespace Cetee.Data;

/// <summary>DbContext chính — kế thừa IdentityDbContext (khóa int) để dùng ASP.NET Core Identity.</summary>
public class AppDbContext : IdentityDbContext<User, Role, int>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Users và Roles đã có sẵn từ IdentityDbContext.
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskAssignee> TaskAssignees => Set<TaskAssignee>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<PasswordResetCode> PasswordResetCodes => Set<PasswordResetCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // cấu hình các bảng Identity

        // Giữ tên bảng "Users"/"Roles" (thay vì AspNetUsers/AspNetRoles) để không phải
        // đổi tên bảng, giữ nguyên mọi khóa ngoại và script seed hiện có.
        modelBuilder.Entity<User>().ToTable("Users");
        modelBuilder.Entity<Role>().ToTable("Roles");

        // Một user có đúng một vai trò qua FK trực tiếp RoleId (song song với Identity roles).
        modelBuilder.Entity<User>()
            .HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Quan hệ quản lý: User (nhân viên) -> Manager (tự tham chiếu User).
        // Restrict để tránh vòng cascade; khi xóa Manager sẽ gỡ liên kết thủ công.
        modelBuilder.Entity<User>()
            .HasOne(u => u.Manager)
            .WithMany(u => u.Subordinates)
            .HasForeignKey(u => u.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);

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

        // Đa phụ trách: bảng nối TaskAssignee (nhiều-nhiều giữa Task và User).
        modelBuilder.Entity<TaskAssignee>()
            .HasKey(ta => new { ta.TaskItemId, ta.UserId });

        // Xóa task -> gỡ luôn các phân công của nó (cascade).
        modelBuilder.Entity<TaskAssignee>()
            .HasOne(ta => ta.TaskItem)
            .WithMany(t => t.Assignees)
            .HasForeignKey(ta => ta.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Xóa user -> KHÔNG cascade (tránh nhiều đường cascade tới cùng bảng);
        // phân công của user sẽ được gỡ thủ công khi xóa user.
        modelBuilder.Entity<TaskAssignee>()
            .HasOne(ta => ta.User)
            .WithMany(u => u.TaskAssignments)
            .HasForeignKey(ta => ta.UserId)
            .OnDelete(DeleteBehavior.Restrict);

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

        // Mã OTP đặt lại mật khẩu thuộc 1 user (xóa user -> xóa mã).
        modelBuilder.Entity<PasswordResetCode>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
