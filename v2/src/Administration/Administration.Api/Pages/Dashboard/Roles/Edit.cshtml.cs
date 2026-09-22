using System.ComponentModel.DataAnnotations;
using Administration.Api.Pages.Dashboard.Projects.Roles;
using Administration.Api.Resources;
using Administration.Domain.Entities;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Platform.Shared.Kernel.Authorization;

namespace Administration.Api.Pages.Dashboard.Roles;

public record RoleMember(Guid UserId, string Email, string FirstName, string LastName);

/// <summary>Edits a global role (Role.ProjectId == null). Compare
/// Pages/Dashboard/Projects/Roles/Edit.cshtml.cs for the project-scoped equivalent - the one
/// difference that matters is assignment: a global role has no project to join, so
/// OnPostAssignUserAsync never touches ProjectMembership.</summary>
public class EditModel(
    UserManager<User> users,
    AdminDbContext db,
    IUserPermissionsRepository permissions,
    IStringLocalizer<SharedResource> localizer,
    ILogger<DashboardPageModel> logger)
    : DashboardPageModel(users, db, permissions, logger)
{
    public Role Role { get; private set; } = null!;
    public IReadOnlyList<string> AllPermissions { get; } = PermissionCatalog.All;
    public List<RoleMember> RoleMembers { get; set; } = [];

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public AssignUserInputModel AssignInput { get; set; } = new();

    public class InputModel
    {
        public string? Description { get; set; }

        public List<string> Permissions { get; set; } = [];
    }

    public class AssignUserInputModel
    {
        [EmailAddress(ErrorMessage = "EmailInvalid")]
        public string? Email { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid roleId)
    {
        if (!CanManageRoles)
            return RequirePermission(false, Permissions.ManageRoles);

        if (!await LoadContextAsync(roleId))
            return NotFound();

        Input = new InputModel
        {
            Description = Role.Description,
            Permissions = await Db.RoleClaims
                .Where(rc => rc.RoleId == roleId && rc.ClaimType == "permission")
                .Select(rc => rc.ClaimValue!)
                .ToListAsync()
        };

        RoleMembers = await LoadMembersAsync(roleId);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid roleId)
    {
        if (!CanManageRoles)
            return RequirePermission(false, Permissions.ManageRoles);

        if (!await LoadContextAsync(roleId))
            return NotFound();

        ModelState.Clear();
        if (!TryValidateModel(Input, nameof(Input)))
        {
            RoleMembers = await LoadMembersAsync(roleId);
            return Page();
        }

        Role.Description = Input.Description;

        var existingClaims = await Db.RoleClaims.Where(rc => rc.RoleId == roleId && rc.ClaimType == "permission").ToListAsync();
        Db.RoleClaims.RemoveRange(existingClaims);

        foreach (var permission in Input.Permissions.Intersect(PermissionCatalog.All))
        {
            Db.RoleClaims.Add(new IdentityRoleClaim<Guid>
            {
                RoleId = roleId,
                ClaimType = "permission",
                ClaimValue = permission
            });
        }

        await Db.SaveChangesAsync();
        Logger.LogInformation("Admin {AdminId} updated permissions for global role {RoleId}", CurrentUser.Id, roleId);

        return RedirectToPage("/Dashboard/Roles/Edit", new { roleId });
    }

    public async Task<IActionResult> OnPostAssignUserAsync(Guid roleId)
    {
        if (!CanManageRoles)
            return RequirePermission(false, Permissions.ManageRoles);

        if (!await LoadContextAsync(roleId))
            return NotFound();

        ModelState.Clear();
        if (!TryValidateModel(AssignInput, nameof(AssignInput)) || string.IsNullOrWhiteSpace(AssignInput.Email))
            return await ReloadFormAsync(roleId);

        var targetUser = await Users.FindByEmailAsync(AssignInput.Email);
        if (targetUser is null)
        {
            ModelState.AddModelError(string.Empty, localizer["UserNotFound"]);
            return await ReloadFormAsync(roleId);
        }

        var alreadyAssigned = await Db.UserRoles.AnyAsync(ur => ur.UserId == targetUser.Id && ur.RoleId == roleId);
        if (alreadyAssigned)
        {
            ModelState.AddModelError(string.Empty, localizer["UserAlreadyHasRole"]);
            return await ReloadFormAsync(roleId);
        }

        // Unlike a project-scoped role, a global role has nothing to "join" - no ProjectMembership
        // row is created here.
        Db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = targetUser.Id, RoleId = roleId });
        await Db.SaveChangesAsync();

        Logger.LogInformation("Admin {AdminId} assigned global role {RoleId} to user {UserId}",
            CurrentUser.Id, roleId, targetUser.Id);

        return RedirectToPage("/Dashboard/Roles/Edit", new { roleId });
    }

    public async Task<IActionResult> OnPostRemoveMemberAsync(Guid roleId, Guid userId)
    {
        if (!CanManageRoles)
            return RequirePermission(false, Permissions.ManageRoles);

        if (!await LoadContextAsync(roleId))
            return NotFound();

        var userRole = await Db.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
        if (userRole is not null)
        {
            Db.UserRoles.Remove(userRole);
            await Db.SaveChangesAsync();
            Logger.LogInformation("Admin {AdminId} removed global role {RoleId} from user {UserId}", CurrentUser.Id, roleId, userId);
        }

        return RedirectToPage("/Dashboard/Roles/Edit", new { roleId });
    }

    private async Task<bool> LoadContextAsync(Guid roleId)
    {
        var role = await Db.Roles.FirstOrDefaultAsync(r => r.Id == roleId && r.ProjectId == null);
        if (role is null)
            return false;

        Role = role;
        return true;
    }

    private async Task<IActionResult> ReloadFormAsync(Guid roleId)
    {
        Input.Description = Role.Description;
        Input.Permissions = await Db.RoleClaims
            .Where(rc => rc.RoleId == roleId && rc.ClaimType == "permission")
            .Select(rc => rc.ClaimValue!)
            .ToListAsync();
        RoleMembers = await LoadMembersAsync(roleId);
        return Page();
    }

    private Task<List<RoleMember>> LoadMembersAsync(Guid roleId) =>
        Db.UserRoles
            .Where(ur => ur.RoleId == roleId)
            .Join(Db.Users, ur => ur.UserId, u => u.Id, (_, u) => u)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Select(u => new RoleMember(u.Id, u.Email!, u.FirstName, u.LastName))
            .ToListAsync();
}
