using Administration.Domain.Entities;
using Administration.Domain.Entities.Enums;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Shared.Kernel.Authorization;

namespace Administration.Api.Pages.Dashboard.Documents;

public record DocumentListItem(
    Guid Id, DocumentType Type, string Title, int Version, DateTime EffectiveAt, bool IsRequired, string? ProjectName);

public class IndexModel(UserManager<User> users, AdminDbContext db, IUserPermissionsRepository permissions, ILogger<DashboardPageModel> logger)
    : DashboardPageModel(users, db, permissions, logger)
{
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public IReadOnlyList<DocumentListItem> DocumentList { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!CanManageDocuments)
            return RequirePermission(false, Permissions.ManageDocuments);

        var query = Db.Documents.Where(d => d.IsActive);

        var term = Search?.Trim();
        if (!string.IsNullOrEmpty(term))
            query = query.Where(d => EF.Functions.ILike(d.Title, $"%{term}%"));

        DocumentList = await query
            .OrderByDescending(d => d.IsRequired)
            .ThenBy(d => d.Type)
            .Select(d => new DocumentListItem(
                d.Id, d.Type, d.Title, d.Version, d.EffectiveAt, d.IsRequired, d.Project == null ? null : d.Project.Name))
            .ToListAsync();

        return Page();
    }
}
