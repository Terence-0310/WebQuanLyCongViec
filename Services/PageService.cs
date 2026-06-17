using Microsoft.EntityFrameworkCore;
using Cetee.Data;
using Cetee.Models;
using Cetee.ViewModels;

namespace Cetee.Services;

public interface IPageService
{
    Task<Page?> GetByIdForUserAsync(int id, int userId, bool seeAll);
    Task<Page?> CreateAsync(PageFormViewModel model, int userId, bool seeAll);
    Task<bool> UpdateAsync(PageFormViewModel model, int userId, bool seeAll);
    Task<int?> DeleteAsync(int id, int userId, bool seeAll); // trả về ProjectId để điều hướng
}

/// <summary>Nghiệp vụ quản lý page ghi chú trong project.</summary>
public class PageService : IPageService
{
    private readonly AppDbContext _db;

    public PageService(AppDbContext db) => _db = db;

    private IQueryable<Page> Accessible(int userId, bool seeAll)
    {
        var query = _db.Pages.AsQueryable();
        return seeAll ? query : query.Where(p => p.Project.Workspace.Members.Any(m => m.UserId == userId));
    }

    // Kiểm tra user có quyền với project chứa page hay không.
    private Task<bool> CanAccessProject(int projectId, int userId, bool seeAll)
    {
        if (seeAll) return _db.Projects.AnyAsync(p => p.Id == projectId);
        return _db.Projects.AnyAsync(p => p.Id == projectId && p.Workspace.Members.Any(m => m.UserId == userId));
    }

    public Task<Page?> GetByIdForUserAsync(int id, int userId, bool seeAll) =>
        Accessible(userId, seeAll).Include(p => p.Project).FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Page?> CreateAsync(PageFormViewModel model, int userId, bool seeAll)
    {
        if (!await CanAccessProject(model.ProjectId, userId, seeAll)) return null;

        var page = new Page
        {
            Title = model.Title.Trim(),
            Content = model.Content,
            ProjectId = model.ProjectId
        };
        _db.Pages.Add(page);
        await _db.SaveChangesAsync();
        return page;
    }

    public async Task<bool> UpdateAsync(PageFormViewModel model, int userId, bool seeAll)
    {
        var page = await GetByIdForUserAsync(model.Id, userId, seeAll);
        if (page is null) return false;

        page.Title = model.Title.Trim();
        page.Content = model.Content;
        page.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int?> DeleteAsync(int id, int userId, bool seeAll)
    {
        var page = await GetByIdForUserAsync(id, userId, seeAll);
        if (page is null) return null;

        int projectId = page.ProjectId;
        _db.Pages.Remove(page);
        await _db.SaveChangesAsync();
        return projectId;
    }
}
