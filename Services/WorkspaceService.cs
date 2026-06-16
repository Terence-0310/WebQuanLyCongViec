using Microsoft.EntityFrameworkCore;
using Cetee.Data;
using Cetee.Models;
using Cetee.ViewModels;

namespace Cetee.Services;

public interface IWorkspaceService
{
    Task<List<Workspace>> GetForUserAsync(int userId, bool isAdmin);
    Task<Workspace?> GetByIdForUserAsync(int id, int userId, bool isAdmin);
    Task<Workspace> CreateAsync(WorkspaceFormViewModel model, int ownerId);
    Task<bool> UpdateAsync(WorkspaceFormViewModel model, int userId, bool isAdmin);
    Task<bool> DeleteAsync(int id, int userId, bool isAdmin);
}

/// <summary>Nghiệp vụ quản lý workspace. User chỉ thấy workspace mình là thành viên; Admin thấy tất cả.</summary>
public class WorkspaceService : IWorkspaceService
{
    private readonly AppDbContext _db;

    public WorkspaceService(AppDbContext db) => _db = db;

    // Lọc workspace mà người dùng được phép truy cập.
    private IQueryable<Workspace> Accessible(int userId, bool isAdmin)
    {
        var query = _db.Workspaces.AsQueryable();
        return isAdmin ? query : query.Where(w => w.Members.Any(m => m.UserId == userId));
    }

    public Task<List<Workspace>> GetForUserAsync(int userId, bool isAdmin) =>
        Accessible(userId, isAdmin)
            .Include(w => w.Owner)
            .Include(w => w.Projects)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

    public Task<Workspace?> GetByIdForUserAsync(int id, int userId, bool isAdmin) =>
        Accessible(userId, isAdmin)
            .Include(w => w.Owner)
            .FirstOrDefaultAsync(w => w.Id == id);

    public async Task<Workspace> CreateAsync(WorkspaceFormViewModel model, int ownerId)
    {
        var workspace = new Workspace
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            OwnerId = ownerId
        };
        // Người tạo tự động trở thành thành viên với vai trò Owner.
        workspace.Members.Add(new WorkspaceMember { UserId = ownerId, Role = MemberRole.Owner });

        _db.Workspaces.Add(workspace);
        await _db.SaveChangesAsync();
        return workspace;
    }

    public async Task<bool> UpdateAsync(WorkspaceFormViewModel model, int userId, bool isAdmin)
    {
        var workspace = await GetByIdForUserAsync(model.Id, userId, isAdmin);
        if (workspace is null) return false;

        workspace.Name = model.Name.Trim();
        workspace.Description = model.Description?.Trim();
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id, int userId, bool isAdmin)
    {
        var workspace = await GetByIdForUserAsync(id, userId, isAdmin);
        if (workspace is null) return false;

        _db.Workspaces.Remove(workspace);
        await _db.SaveChangesAsync();
        return true;
    }
}
