using Administration.Api.Pages.Dashboard;
using Administration.Domain.Entities;
using Administration.Domain.Entities.Enums;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Shared.Kernel.Authorization;

namespace Administration.Api.Pages.Dashboard.Projects;

public record ProjectListItem(Guid Id, string Name, string Domain, ProjectState State);

public class IndexModel(UserManager<User> users, AdminDbContext db, IUserPermissionsRepository permissions, ILogger<DashboardPageModel> logger)
    : DashboardPageModel(users, db, permissions, logger)
{
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SearchField { get; set; } = "name";

    [BindProperty(SupportsGet = true)]
    public string? Sort { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Dir { get; set; } = "asc";

    public IReadOnlyList<ProjectListItem> ProjectList { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!CanManageProjects)
            return RequirePermission(false, Permissions.ManageProjects);

        var query = Db.Projects.AsQueryable();

        var term = Search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            query = SearchField == "domain"
                ? query.Where(p => EF.Functions.ILike(p.Domain, $"%{term}%"))
                : query.Where(p => EF.Functions.ILike(p.Name, $"%{term}%"));
        }

        var descending = Dir == "desc";
        query = (Sort, descending) switch
        {
            ("domain", true) => query.OrderByDescending(p => p.Domain),
            ("domain", false) => query.OrderBy(p => p.Domain),
            ("status", true) => query.OrderByDescending(p => p.State),
            ("status", false) => query.OrderBy(p => p.State),
            (_, true) => query.OrderByDescending(p => p.Name),
            (_, false) => query.OrderBy(p => p.Name)
        };

        ProjectList = await query
            .Select(p => new ProjectListItem(p.Id, p.Name, p.Domain, p.State))
            .ToListAsync();

        return Page();
    }
}
