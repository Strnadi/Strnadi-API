using Administration.Domain.Entities;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Shared.Kernel.Authorization;

namespace Administration.Api.Pages.Dashboard.Projects.Roles;

public record RoleListItem(Guid Id, string Name, string? Description, int PermissionCount, int MemberCount);

public class IndexModel(UserManager<User> users, AdminDbContext db, IUserPermissionsRepository permissions, ILogger<DashboardPageModel> logger)
    : DashboardPageModel(users, db, permissions, logger)
{
    public Project Project { get; private set; } = null!;
    public IReadOnlyList<RoleListItem> RoleList { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid projectId)
    {
        if (!CanManageRoles)
            return RequirePermission(false, Permissions.ManageRoles);

        var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null)
            return NotFound();

        Project = project;

        RoleList = await Db.Roles
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.Name)
            .Select(r => new RoleListItem(
                r.Id,
                r.Name!,
                r.Description,
                Db.RoleClaims.Count(rc => rc.RoleId == r.Id && rc.ClaimType == "permission"),
                Db.UserRoles.Count(ur => ur.RoleId == r.Id)))
            .ToListAsync();

        return Page();
    }
}
