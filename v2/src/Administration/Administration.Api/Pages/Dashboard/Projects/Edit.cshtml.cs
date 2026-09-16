using System.ComponentModel.DataAnnotations;
using Administration.Api.Resources;
using Administration.Domain.Entities;
using Administration.Domain.Entities.Enums;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Platform.Shared.Kernel.Authorization;
using Platform.Shared.Kernel.Services;

namespace Administration.Api.Pages.Dashboard.Projects;

public class EditModel(
    UserManager<User> users,
    AdminDbContext db,
    IUserPermissionsRepository permissions,
    ILogger<DashboardPageModel> logger,
    IStringLocalizer<SharedResource> localizer,
    IFileStorage fileStorage)
    : DashboardPageModel(users, db, permissions, logger)
{
    public Project TargetProject { get; private set; } = null!;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "FieldRequired")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required(ErrorMessage = "FieldRequired")]
        public string Domain { get; set; } = string.Empty;

        public string? ApiDomain { get; set; }

        public ProjectState State { get; set; }

        public string? RejectionReason { get; set; }

        public IFormFile? Logo { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!CanManageProjects)
            return RequirePermission(false, Permissions.ManageProjects);

        var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == id);
        if (project is null)
            return NotFound();

        TargetProject = project;
        Input = new InputModel
        {
            Name = project.Name,
            Description = project.Description,
            Domain = project.Domain,
            ApiDomain = project.ApiDomain,
            State = project.State,
            RejectionReason = project.RejectionReason
        };

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

        if (Input.State == ProjectState.Rejected && string.IsNullOrWhiteSpace(Input.RejectionReason))
            ModelState.AddModelError(nameof(Input.RejectionReason), localizer["FieldRequired"]);

        if (!ModelState.IsValid)
            return Page();

        if (!string.Equals(Input.Domain, project.Domain, StringComparison.OrdinalIgnoreCase)
            && await Db.Projects.AnyAsync(p => p.Id != id && p.Domain == Input.Domain))
        {
            ModelState.AddModelError(nameof(Input.Domain), localizer["DomainAlreadyTaken"]);
            return Page();
        }

        var wasActive = project.State == ProjectState.Active;

        project.Name = Input.Name;
        project.Description = Input.Description;
        project.Domain = Input.Domain;
        project.ApiDomain = Input.ApiDomain;
        project.State = Input.State;
        project.RejectionReason = Input.State == ProjectState.Rejected ? Input.RejectionReason : null;

        if (!wasActive && Input.State == ProjectState.Active)
        {
            project.ApprovedBy = CurrentUser.Id;
            project.ApprovedAt = DateTime.UtcNow;
        }

        if (Input.Logo is { Length: > 0 })
        {
            var format = Path.GetExtension(Input.Logo.FileName).TrimStart('.').ToLowerInvariant();
            using var stream = new MemoryStream();
            await Input.Logo.CopyToAsync(stream);
            project.PhotoPath = await fileStorage.SaveAsync($"projects/{project.Id}.{format}", stream.ToArray());
            project.PhotoFormat = format;
        }

        await Db.SaveChangesAsync();

        Logger.LogInformation("Admin {AdminId} updated project {ProjectId} (state {State})", CurrentUser.Id, project.Id, project.State);
        return RedirectToPage("/Dashboard/Projects/Details", new { id = project.Id });
    }
}
