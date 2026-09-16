using System.ComponentModel.DataAnnotations;
using Administration.Api.Resources;
using Administration.Domain.Entities;
using Administration.Domain.Entities.Enums;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Platform.Shared.Kernel.Authorization;

namespace Administration.Api.Pages.Dashboard.Projects;

public class CreateModel(
    UserManager<User> users,
    AdminDbContext db,
    IUserPermissionsRepository permissions,
    ILogger<DashboardPageModel> logger,
    IStringLocalizer<SharedResource> localizer)
    : DashboardPageModel(users, db, permissions, logger)
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "FieldRequired")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "FieldRequired")]
        public string Domain { get; set; } = string.Empty;
    }

    public IActionResult OnGet() => RequirePermission(CanManageProjects, Permissions.ManageProjects);

    public async Task<IActionResult> OnPostAsync()
    {
        if (!CanManageProjects)
            return RequirePermission(false, Permissions.ManageProjects);

        if (!ModelState.IsValid)
            return Page();

        if (await Db.Projects.AnyAsync(p => p.Domain == Input.Domain))
        {
            ModelState.AddModelError(nameof(Input.Domain), localizer["DomainAlreadyTaken"]);
            return Page();
        }

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = Input.Name,
            Domain = Input.Domain,
            State = ProjectState.Draft
        };

        Db.Projects.Add(project);
        await Db.SaveChangesAsync();

        Logger.LogInformation("Admin {AdminId} created project {ProjectId}", CurrentUser.Id, project.Id);

        return RedirectToPage("/Dashboard/Projects/Details", new { id = project.Id });
    }
}
