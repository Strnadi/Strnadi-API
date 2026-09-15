using System.ComponentModel.DataAnnotations;
using Administration.Domain.Entities;
using Administration.Domain.Persistence.Repositories;
using Administration.Domain.Services;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Shared.Kernel.Authorization;

namespace Administration.Api.Pages.Dashboard.Documents;

public class EditModel(
    UserManager<User> users,
    AdminDbContext db,
    IUserPermissionsRepository permissions,
    IDocumentEmailSender documentEmailSender,
    ILogger<DashboardPageModel> logger)
    : DashboardPageModel(users, db, permissions, logger)
{
    public Document CurrentDocument { get; private set; } = null!;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "FieldRequired")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "FieldRequired")]
        public string Content { get; set; } = string.Empty;

        public bool IsRequired { get; set; } = true;

        public DateTime? EffectiveAt { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!CanManageDocuments)
            return RequirePermission(false, Permissions.ManageDocuments);

        var document = await Db.Documents.FindAsync(id);
        if (document is null || !document.IsActive)
            return NotFound();

        CurrentDocument = document;
        Input = new InputModel
        {
            Title = document.Title,
            Content = document.Content,
            IsRequired = document.IsRequired
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!CanManageDocuments)
            return RequirePermission(false, Permissions.ManageDocuments);

        var current = await Db.Documents.FindAsync(id);
        if (current is null || !current.IsActive)
            return NotFound();

        CurrentDocument = current;

        if (!ModelState.IsValid)
            return Page();

        current.IsActive = false;

        var publishedAt = DateTime.UtcNow;

        var next = new Document
        {
            Id = Guid.NewGuid(),
            Type = current.Type,
            Title = Input.Title,
            Version = current.Version + 1,
            Content = Input.Content,
            PublishedAt = publishedAt,
            EffectiveAt = Input.EffectiveAt ?? publishedAt,
            IsActive = true,
            IsRequired = Input.IsRequired,
            ProjectId = current.ProjectId
        };

        Db.Documents.Add(next);
        await Db.SaveChangesAsync();

        Logger.LogInformation("Admin {AdminId} published document {DocumentId} as version {Version} (supersedes {PreviousId})",
            CurrentUser.Id, next.Id, next.Version, current.Id);

        await NotifyAffectedUsersAsync(current, next);

        return RedirectToPage("/Dashboard/Documents/Index");
    }

    /// <summary>Emails everyone who had accepted the previous version, since that acceptance no longer covers the new one.</summary>
    private async Task NotifyAffectedUsersAsync(Document previousVersion, Document newVersion)
    {
        var affectedUsers = await Db.DocumentAcceptances
            .Where(da => da.DocumentId == previousVersion.Id && da.RevokedAt == null)
            .Join(Db.Users, da => da.UserId, u => u.Id, (_, u) => u)
            .Where(u => u.Email != null)
            .ToListAsync();

        if (affectedUsers.Count == 0)
            return;

        var documentLink = $"{Request.Scheme}://{Request.Host}/dashboard";

        foreach (var user in affectedUsers)
            await documentEmailSender.SendDocumentUpdatedAsync(user, user.Email!, newVersion, documentLink);

        Logger.LogInformation("Notified {UserCount} users about the new version of document {DocumentId}",
            affectedUsers.Count, newVersion.Id);
    }
}
