using System.ComponentModel.DataAnnotations;
using Administration.Api.Resources;
using Administration.Domain.Authorization;
using Administration.Domain.Entities;
using Administration.Domain.Entities.Enums;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Administration.Api.Pages.Dashboard.Projects;

public class EditModel(
    UserManager<User> users,
    AdminDbContext db,
    IUserPermissionsRepository permissions,
    ILogger<DashboardPageModel> logger,
    IStringLocalizer<SharedResource> localizer)
    : DashboardPageModel(users, db, permissions, logger)
{
    public Project TargetProject { get; private set; } = null!;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "FieldRequired")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "FieldRequired")]
        public string Domain { get; set; } = string.Empty;

        public ProjectState State { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!CanManageProjects)
            return RequirePermission(false, Permissions.ManageProjects);

        var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == id);
        if (project is null)
            return NotFound();

        TargetProject = project;
        Input = new InputModel { Name = project.Name, Domain = project.Domain, State = project.State };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!CanManageProjects)
            return RequirePermission(false, Permissions.ManageProjects);

        var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == id);
        if (project is null)
            return NotFound();

        TargetProject = project;

        if (!ModelState.IsValid)
            return Page();

        if (!string.Equals(Input.Domain, project.Domain, StringComparison.OrdinalIgnoreCase)
            && await Db.Projects.AnyAsync(p => p.Id != id && p.Domain == Input.Domain))
        {
            ModelState.AddModelError(nameof(Input.Domain), localizer["DomainAlreadyTaken"]);
            return Page();
        }

        project.Name = Input.Name;
        project.Domain = Input.Domain;
        project.State = Input.State;

        await Db.SaveChangesAsync();

        Logger.LogInformation("Admin {AdminId} updated project {ProjectId}", CurrentUser.Id, project.Id);
        return RedirectToPage("/Dashboard/Projects/Details", new { id = project.Id });
    }
}
