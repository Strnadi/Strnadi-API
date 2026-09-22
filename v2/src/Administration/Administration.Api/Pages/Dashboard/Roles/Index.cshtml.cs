using Administration.Domain.Entities;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Shared.Kernel.Authorization;

namespace Administration.Api.Pages.Dashboard.Roles;

public record RoleListItem(Guid Id, string Name, string? Description, int PermissionCount, int MemberCount);

/// <summary>Lists global roles (Role.ProjectId == null) - roles that act everywhere, not just in
/// one project. Compare Pages/Dashboard/Projects/Roles/Index.cshtml.cs for the project-scoped equivalent.</summary>
public class IndexModel(UserManager<User> users, AdminDbContext db, IUserPermissionsRepository permissions, ILogger<DashboardPageModel> logger)
    : DashboardPageModel(users, db, permissions, logger)
{
    public IReadOnlyList<RoleListItem> RoleList { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!CanManageRoles)
            return RequirePermission(false, Permissions.ManageRoles);

        RoleList = await Db.Roles
            .Where(r => r.ProjectId == null)
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
