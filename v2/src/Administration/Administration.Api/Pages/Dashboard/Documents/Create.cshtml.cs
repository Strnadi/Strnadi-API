using System.ComponentModel.DataAnnotations;
using Administration.Domain.Entities;
using Administration.Domain.Entities.Enums;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Shared.Kernel.Authorization;

namespace Administration.Api.Pages.Dashboard.Documents;

public class CreateModel(UserManager<User> users, AdminDbContext db, IUserPermissionsRepository permissions, ILogger<DashboardPageModel> logger)
    : DashboardPageModel(users, db, permissions, logger)
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public DocumentType Type { get; set; }

        [Required(ErrorMessage = "FieldRequired")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "FieldRequired")]
        public string Content { get; set; } = string.Empty;

        public bool IsRequired { get; set; } = true;

        public DateTime? EffectiveAt { get; set; }
    }

    public IActionResult OnGet()
    {
        if (!CanManageDocuments)
            return RequirePermission(false, Permissions.ManageDocuments);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!CanManageDocuments)
            return RequirePermission(false, Permissions.ManageDocuments);

        if (!ModelState.IsValid)
            return Page();

        // Document currently only manages platform-wide (non-project-scoped) documents from this screen.
        var exists = await Db.Documents.AnyAsync(d => d.Type == Input.Type && d.ProjectId == null && d.IsActive);
        if (exists)
        {
            ModelState.AddModelError(string.Empty, $"An active document of type {Input.Type} already exists.");
            return Page();
        }

        var publishedAt = DateTime.UtcNow;

        // The client converts the local datetime-local input to a UTC instant (ISO string with an
        // explicit offset) before submitting - ToUniversalTime() here just normalizes whatever Kind
        // model binding produced from that string into a true UTC DateTime for Npgsql.
        var effectiveAt = Input.EffectiveAt is null
            ? publishedAt
            : Input.EffectiveAt.Value.ToUniversalTime();

        var document = new Document
        {
            Id = Guid.NewGuid(),
            Type = Input.Type,
            Title = Input.Title,
            Version = 1,
            Content = Input.Content,
            PublishedAt = publishedAt,
            EffectiveAt = effectiveAt,
            IsActive = true,
            IsRequired = Input.IsRequired,
            ProjectId = null
        };

        Db.Documents.Add(document);
        await Db.SaveChangesAsync();

        Logger.LogInformation("Admin {AdminId} created document {DocumentId} of type {Type}", CurrentUser.Id, document.Id, document.Type);

        return RedirectToPage("/Dashboard/Documents/Index");
    }
}
