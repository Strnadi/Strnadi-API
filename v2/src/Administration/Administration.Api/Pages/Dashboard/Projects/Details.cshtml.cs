using Administration.Api.Pages.Dashboard;
using Administration.Api.Services;
using Administration.Domain.Entities;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Shared.Kernel.Authorization;
using Platform.Shared.Kernel.Services;

namespace Administration.Api.Pages.Dashboard.Projects;

public class DetailsModel(
    UserManager<User> users,
    AdminDbContext db,
    IUserPermissionsRepository permissions,
    ILogger<DashboardPageModel> logger,
    IFileStorage fileStorage,
    ITenantConformanceChecker conformanceChecker)
    : DashboardPageModel(users, db, permissions, logger)
{
    public Project Project { get; private set; } = null!;
    public IReadOnlyList<string> RoleNames { get; private set; } = [];
    public string? CreatedByName { get; private set; }
    public IReadOnlyList<string> DeclaredFeatures { get; private set; } = [];
    public IReadOnlyList<string> MissingFeatures { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var (project, roleNames, denied) = await LoadAsync(id);
        if (denied)
            return NotFound();

        Project = project!;
        RoleNames = roleNames;

        if (project!.CreatedBy is { } createdBy)
        {
            var creator = await Users.FindByIdAsync(createdBy.ToString());
            CreatedByName = creator is null ? null : $"{creator.FirstName} {creator.LastName}";
        }

        DeclaredFeatures = conformanceChecker.ParseFeatures(project.CapabilitiesJson);

        // Fetching our own reference deployment (not the untrusted ApiDomain) so this comparison
        // can run on every page view, unlike the two on-demand checks below.
        if (CanManageProjects && DeclaredFeatures.Count > 0)
        {
            var ownFeatures = await conformanceChecker.FetchOwnFeaturesAsync();
            if (ownFeatures is not null)
                MissingFeatures = ownFeatures.Except(DeclaredFeatures).ToList();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostRefreshCapabilitiesAsync(Guid id)
    {
        if (!CanManageProjects)
            return RequirePermission(false, Permissions.ManageProjects);

        var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == id);
        if (project?.ApiDomain is null)
            return NotFound();

        project.CapabilitiesJson = await conformanceChecker.FetchCapabilitiesAsync(project.ApiDomain);
        project.CapabilitiesCheckedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCheckApiConformanceAsync(Guid id)
    {
        if (!CanManageProjects)
            return RequirePermission(false, Permissions.ManageProjects);

        var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == id);
        if (project?.ApiDomain is null)
            return NotFound();

        var missing = await conformanceChecker.CheckApiConformanceAsync(project.ApiDomain);
        project.ApiConformanceReport = string.Join('\n', missing);
        project.ApiConformanceCheckedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnGetLogoAsync(Guid id)
    {
        var (project, _, denied) = await LoadAsync(id);
        if (denied || project?.PhotoPath is null || project.PhotoFormat is null)
            return NotFound();

        var content = await fileStorage.ReadAsync(project.PhotoPath);
        if (content is null)
            return NotFound();

        return File(content, $"image/{project.PhotoFormat}");
    }

    // Same access rule for both the page and the logo image it embeds - a Draft/Rejected
    // project's logo is exactly the kind of not-yet-reviewed content that shouldn't be fetchable
    // by URL just because someone knows/guesses its id.
    private async Task<(Project? Project, IReadOnlyList<string> RoleNames, bool Denied)> LoadAsync(Guid id)
    {
        var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == id);
        if (project is null)
        {
            Logger.LogWarning("Project {ProjectId} not found for user {UserId}", id, CurrentUser.Id);
            return (null, [], true);
        }

        var roleNames = await Db.UserRoles
            .Where(ur => ur.UserId == CurrentUser.Id)
            .Join(Db.Roles.Where(r => r.ProjectId == id), ur => ur.RoleId, r => r.Id, (_, r) => r.Name!)
            .ToListAsync();

        // A global project/role admin can manage a project they aren't personally a member of;
        // everyone else needs an actual role in this specific project to see it at all.
        if (roleNames.Count == 0 && !CanManageProjects && !CanManageRoles)
        {
            Logger.LogWarning("Denied user {UserId} access to project {ProjectId} - no role in that project", CurrentUser.Id, id);
            return (project, roleNames, true);
        }

        return (project, roleNames, false);
    }
}
