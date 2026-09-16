using Administration.Api.Pages.Dashboard;
using Administration.Domain.Entities;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Shared.Kernel.Services;

namespace Administration.Api.Pages.Dashboard.Projects;

public class DetailsModel(
    UserManager<User> users,
    AdminDbContext db,
    IUserPermissionsRepository permissions,
    ILogger<DashboardPageModel> logger,
    IFileStorage fileStorage)
    : DashboardPageModel(users, db, permissions, logger)
{
    public Project Project { get; private set; } = null!;
    public IReadOnlyList<string> RoleNames { get; private set; } = [];
    public string? CreatedByName { get; private set; }

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

        return Page();
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
