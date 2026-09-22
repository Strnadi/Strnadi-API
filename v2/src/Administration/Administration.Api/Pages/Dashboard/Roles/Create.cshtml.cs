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

public class CreateModel(
    UserManager<User> users,
    AdminDbContext db,
    IUserPermissionsRepository permissions,
    IStringLocalizer<SharedResource> localizer,
    ILogger<DashboardPageModel> logger)
    : DashboardPageModel(users, db, permissions, logger)
{
    public IReadOnlyList<string> AllPermissions { get; } = PermissionCatalog.All;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "FieldRequired")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public List<string> Permissions { get; set; } = [];
    }

    public IActionResult OnGet()
    {
        if (!CanManageRoles)
            return RequirePermission(false, Permissions.ManageRoles);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!CanManageRoles)
            return RequirePermission(false, Permissions.ManageRoles);

        if (!ModelState.IsValid)
            return Page();

        var normalizedName = Input.Name.ToUpperInvariant();
        var nameTaken = await Db.Roles.AnyAsync(r => r.ProjectId == null && r.NormalizedName == normalizedName);
        if (nameTaken)
        {
            ModelState.AddModelError(string.Empty, localizer["RoleNameTaken"]);
            return Page();
        }

        var role = new Role
        {
            Id = Guid.CreateVersion7(),
            Name = Input.Name,
            NormalizedName = normalizedName,
            ProjectId = null,
            Description = Input.Description
        };

        Db.Roles.Add(role);

        foreach (var permission in Input.Permissions.Intersect(PermissionCatalog.All))
        {
            Db.RoleClaims.Add(new IdentityRoleClaim<Guid>
            {
                RoleId = role.Id,
                ClaimType = "permission",
                ClaimValue = permission
            });
        }

        await Db.SaveChangesAsync();

        Logger.LogInformation("Admin {AdminId} created global role {RoleId} ({RoleName})", CurrentUser.Id, role.Id, role.Name);

        return RedirectToPage("/Dashboard/Roles/Index");
    }
}
