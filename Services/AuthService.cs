using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Cetee.Data;
using Cetee.Models;
using Cetee.ViewModels;
using TaskStatus = Cetee.Models.TaskStatus;

namespace Cetee.Services;

public interface IAuthService
{
    Task<(bool Success, string? Error, User? User)> RegisterAsync(RegisterViewModel model);

    /// <summary>Đăng nhập ngoài (Google): tìm user theo email, chưa có thì tạo mới
    /// (vai trò User, tài khoản Cá nhân). Trả về user.</summary>
    Task<User> FindOrCreateExternalUserAsync(string email, string fullName);

    /// <summary>Tạo tài khoản DÙNG THỬ tức thì (cá nhân) kèm dữ liệu mẫu để khám phá ngay.</summary>
    Task<User> CreateTrialAsync();

    /// <summary>Xóa sạch một tài khoản dùng thử + toàn bộ dữ liệu của nó (gọi khi đăng xuất).
    /// Không làm gì nếu user không phải tài khoản dùng thử.</summary>
    Task DeleteTrialAsync(int userId);

    /// <summary>Dọn các tài khoản dùng thử bị bỏ quên (tạo quá 1 ngày) — gọi khi app khởi động.</summary>
    Task CleanupStaleTrialsAsync();
}

/// <summary>Nghiệp vụ đăng ký tài khoản và tạo tài khoản đăng nhập ngoài (qua Identity UserManager).</summary>
public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly UserManager<User> _userManager;

    public AuthService(AppDbContext db, UserManager<User> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<(bool Success, string? Error, User? User)> RegisterAsync(RegisterViewModel model)
    {
        string email = model.Email.Trim().ToLowerInvariant();
        if (await _userManager.FindByEmailAsync(email) is not null)
            return (false, "Email đã được sử dụng.", null);

        var userRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == Roles.User);
        if (userRole is null)
            return (false, "Hệ thống chưa khởi tạo vai trò.", null);

        var user = new User
        {
            FullName = model.FullName.Trim(),
            Email = email,
            UserName = email,
            EmailConfirmed = true,
            RoleId = userRole.Id,
            AccountType = AccountType.Personal // Tự đăng ký = tài khoản cá nhân.
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
            return (false, string.Join("; ", result.Errors.Select(e => e.Description)), null);

        return (true, null, user);
    }

    public async Task<User> FindOrCreateExternalUserAsync(string email, string fullName)
    {
        email = email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(email);
        if (user is not null) return user;

        var userRole = await _db.Roles.FirstAsync(r => r.Name == Roles.User);
        user = new User
        {
            FullName = string.IsNullOrWhiteSpace(fullName) ? email : fullName.Trim(),
            Email = email,
            UserName = email,
            EmailConfirmed = true,
            RoleId = userRole.Id,
            AccountType = AccountType.Personal // Đăng nhập Google = tài khoản cá nhân.
        };
        // Tạo không cần mật khẩu (đăng nhập bằng Google).
        await _userManager.CreateAsync(user);
        return user;
    }

    public async Task<User> CreateTrialAsync()
    {
        // Mỗi khách thử có sandbox RIÊNG (email trial-...@cetee.demo), sẽ tự xóa khi đăng xuất.
        var userRole = await _db.Roles.FirstAsync(r => r.Name == Roles.User);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"trial-{suffix}@cetee.demo";
        var user = new User
        {
            FullName = "Người dùng dùng thử",
            Email = email,
            UserName = email,
            EmailConfirmed = true,
            RoleId = userRole.Id,
            AccountType = AccountType.Personal,
            CreatedAt = DateTime.UtcNow
        };
        var result = await _userManager.CreateAsync(user, "Trial@" + Guid.NewGuid().ToString("N")[..10]);
        if (!result.Succeeded)
            throw new InvalidOperationException("Không tạo được tài khoản dùng thử: " +
                string.Join("; ", result.Errors.Select(e => e.Description)));

        // ---- Dữ liệu mẫu để trải nghiệm ngay (workspace + project + vài task của chính họ) ----
        var now = DateTime.UtcNow;
        var today = DateTime.Today;

        var ws = new Workspace
        {
            Name = "Không gian dùng thử",
            Description = "Workspace mẫu được tạo sẵn để bạn khám phá Cetee.",
            OwnerId = user.Id,
            CreatedAt = now
        };
        ws.Members.Add(new WorkspaceMember { UserId = user.Id, Role = MemberRole.Owner, JoinedAt = now });

        var project = new Project
        {
            Name = "Dự án mẫu",
            Description = "Một dự án mẫu với vài công việc để bạn bắt đầu.",
            Workspace = ws,
            CreatedAt = now
        };
        project.Members.Add(new ProjectMember { UserId = user.Id, Role = MemberRole.Owner, JoinedAt = now });
        ws.Projects.Add(project);

        void AddTask(string title, TaskPriority pri, TaskStatus st, DateTime? scheduled, int dur, DateTime? due)
        {
            var t = new TaskItem
            {
                Title = title, Project = project, Priority = pri, Status = st,
                ScheduledStart = scheduled, DurationMinutes = dur, DueDate = due, CreatedAt = now
            };
            t.Assignees.Add(new TaskAssignee { UserId = user.Id });
            project.Tasks.Add(t);
        }
        AddTask("Khám phá bảng Kanban", TaskPriority.Medium, TaskStatus.Doing, today.AddHours(9), 60, today.AddDays(2));
        AddTask("Thử xếp lịch trên Lịch ngày", TaskPriority.High, TaskStatus.Todo, today.AddHours(11), 90, today.AddDays(1));
        AddTask("Tạo dự án / mời thành viên", TaskPriority.Low, TaskStatus.Todo, null, 60, today.AddDays(5));
        AddTask("Hoàn thành hướng dẫn bắt đầu", TaskPriority.Medium, TaskStatus.Done, today.AddDays(-1).AddHours(14), 30, null);

        _db.Workspaces.Add(ws);
        await _db.SaveChangesAsync();
        return user;
    }

    private static bool IsTrial(User u) =>
        !string.IsNullOrEmpty(u.Email) && u.Email.StartsWith("trial-") && u.Email.EndsWith("@cetee.demo");

    public async Task DeleteTrialAsync(int userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null || !IsTrial(user)) return; // chỉ xóa tài khoản dùng thử

        // Xóa workspace user sở hữu -> cascade project/page/task/assignee/comment/member.
        var owned = await _db.Workspaces.Where(w => w.OwnerId == userId).ToListAsync();
        if (owned.Count > 0)
        {
            _db.Workspaces.RemoveRange(owned);
            await _db.SaveChangesAsync();
        }

        // Gỡ mọi tham chiếu Restrict còn sót tới user (an toàn) trước khi xóa user.
        await _db.TaskAssignees.Where(a => a.UserId == userId).ExecuteDeleteAsync();
        await _db.TaskComments.Where(c => c.UserId == userId).ExecuteDeleteAsync();
        await _db.WorkspaceMembers.Where(m => m.UserId == userId).ExecuteDeleteAsync();
        await _db.ProjectMembers.Where(m => m.UserId == userId).ExecuteDeleteAsync();

        // Xóa user -> cascade notification, activity log, password reset code.
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
    }

    public async Task CleanupStaleTrialsAsync()
    {
        var cutoff = DateTime.UtcNow.AddDays(-1);
        var ids = await _db.Users
            .Where(u => u.Email != null && u.Email.StartsWith("trial-") && u.Email.EndsWith("@cetee.demo")
                        && u.CreatedAt < cutoff)
            .Select(u => u.Id)
            .ToListAsync();
        foreach (var id in ids)
            await DeleteTrialAsync(id);
    }
}
