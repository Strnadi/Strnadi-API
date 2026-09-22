using System.Security.Claims;
using Administration.Domain.Entities;
using Administration.Domain.Entities.Enums;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Platform.Shared.Kernel.Authorization;

namespace Administration.Api.Pages.Dashboard;

public record ProjectSummary(Guid Id, string Name, string Domain, ProjectState State);

[Authorize]
public abstract class DashboardPageModel(
    UserManager<User> users,
    AdminDbContext db,
    IUserPermissionsRepository permissions,
    ILogger<DashboardPageModel> logger) : PageModel
{
    public User CurrentUser { get; private set; } = null!;
    public bool CanManageUsers { get; private set; }
    public bool CanManageProjects { get; private set; }
    public bool CanManageDocuments { get; private set; }
    public bool CanManageRoles { get; private set; }
    public bool CanViewUsersBasic { get; private set; }
    public bool CanViewUsersConfidential { get; private set; }
    public IReadOnlyList<ProjectSummary> Projects { get; private set; } = [];

    protected AdminDbContext Db => db;
    protected UserManager<User> Users => users;
    protected ILogger<DashboardPageModel> Logger => logger;
    // Named PermissionsRepository, not Permissions - a member with that name here would shadow the
    // static Platform.Shared.Kernel.Authorization.Permissions class every derived page already
    // references as Permissions.ManageRoles etc.
    protected IUserPermissionsRepository PermissionsRepository => permissions;

    protected IActionResult RequirePermission(bool granted, string permission)
    {
        if (granted)
            return Page();

        logger.LogWarning("Denied user {UserId} access to {Path} - missing permission {Permission}",
            CurrentUser.Id, Request.Path, permission);
        return RedirectToPage("/Dashboard/AccessDenied");
    }

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = userId is null ? null : await users.FindByIdAsync(userId);
        if (user is null)
        {
            context.Result = RedirectToPage("/Account/Login", new { returnUrl = Request.Path + Request.QueryString });
            return;
        }

        CurrentUser = user;
        CanManageUsers = await permissions.HasPermissionAsync(user.Id, Permissions.ManageUsers);
        CanManageProjects = await permissions.HasPermissionAsync(user.Id, Permissions.ManageProjects);
        CanManageDocuments = await permissions.HasPermissionAsync(user.Id, Permissions.ManageDocuments);
        CanManageRoles = await permissions.HasPermissionAsync(user.Id, Permissions.ManageRoles);
        CanViewUsersBasic = CanManageUsers || await permissions.HasPermissionAsync(user.Id, Permissions.ViewUsersBasic);
        CanViewUsersConfidential = CanManageUsers || await permissions.HasPermissionAsync(user.Id, Permissions.ViewUsersConfidential);

        // A global role's ProjectId is null - it has no project to contribute to this list (that's
        // what /dashboard/roles is for), so it's filtered out before joining to Projects.
        Projects = await db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.ProjectId)
            .Where(projectId => projectId != null)
            .Select(projectId => projectId!.Value)
            .Distinct()
            .Join(db.Projects, projectId => projectId, p => p.Id, (_, p) => new ProjectSummary(p.Id, p.Name, p.Domain, p.State))
            .ToListAsync();

        await next();
    }
}
