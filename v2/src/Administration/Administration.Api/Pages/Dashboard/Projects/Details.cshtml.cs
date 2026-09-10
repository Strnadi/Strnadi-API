using Administration.Domain.Entities;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Administration.Api.Pages.Dashboard.Projects;

public class DetailsModel(UserManager<User> users, AdminDbContext db, IUserPermissionsRepository permissions)
    : DashboardPageModel(users, db, permissions)
{
    public Project Project { get; private set; } = null!;
    public IReadOnlyList<string> RoleNames { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == id);
        if (project is null)
            return NotFound();

        var roleNames = await Db.UserRoles
            .Where(ur => ur.UserId == CurrentUser.Id)
            .Join(Db.Roles.Where(r => r.ProjectId == id), ur => ur.RoleId, r => r.Id, (_, r) => r.Name!)
            .ToListAsync();

        if (roleNames.Count == 0)
            return NotFound();

        Project = project;
        RoleNames = roleNames;
        return Page();
    }
}
