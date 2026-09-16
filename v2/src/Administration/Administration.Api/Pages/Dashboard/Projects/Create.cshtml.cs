using System.ComponentModel.DataAnnotations;
using Administration.Domain.Entities;
using Administration.Domain.Entities.Enums;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Platform.Shared.Kernel.Services;

namespace Administration.Api.Pages.Dashboard.Projects;

// Open to any authenticated user (no CanManageProjects gate) - this is the "submit a request to
// create your own project" entry point, reachable from /dashboard's Projects card as well as the
// admin-only /dashboard/projects list's "+ New project" button. Either way it just inserts a
// Draft Project; nothing here decides who reviews it, that's Edit.cshtml.cs's job.
public class CreateModel(
    UserManager<User> users,
    AdminDbContext db,
    IUserPermissionsRepository permissions,
    ILogger<DashboardPageModel> logger,
    IFileStorage fileStorage)
    : DashboardPageModel(users, db, permissions, logger)
{
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

        public IFormFile? Logo { get; set; }
    }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        // No domain-uniqueness check here on purpose - telling an arbitrary, unauthenticated-
        // for-this-purpose submitter "that domain is already taken" would leak which domains
        // exist. The admin hits the real uniqueness check (Edit.cshtml.cs) when approving.

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = Input.Name,
            Description = Input.Description,
            Domain = Input.Domain,
            ApiDomain = Input.ApiDomain,
            State = ProjectState.Draft,
            CreatedBy = CurrentUser.Id,
            CreatedAt = DateTime.UtcNow
        };

        if (Input.Logo is { Length: > 0 })
        {
            var format = Path.GetExtension(Input.Logo.FileName).TrimStart('.').ToLowerInvariant();
            using var stream = new MemoryStream();
            await Input.Logo.CopyToAsync(stream);
            project.PhotoPath = await fileStorage.SaveAsync($"projects/{project.Id}.{format}", stream.ToArray());
            project.PhotoFormat = format;
        }

        Db.Projects.Add(project);
        await Db.SaveChangesAsync();

        Logger.LogInformation("User {UserId} submitted project request {ProjectId}", CurrentUser.Id, project.Id);

        return RedirectToPage("/Dashboard/Projects/RequestSubmitted");
    }
}
