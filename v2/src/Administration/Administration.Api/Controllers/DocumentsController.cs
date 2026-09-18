using System.Globalization;
using System.Net;
using System.Security.Claims;
using Administration.Application.Documents;
using Administration.Domain.Entities;
using Administration.Domain.Entities.Enums;
using Administration.Domain.Persistence.Repositories;
using Administration.Domain.Services;
using Administration.Infrastructure.Persistence;
using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Platform.Shared.Kernel.Authorization;

namespace Administration.Api.Controllers;

[ApiController]
[Route("documents")]
public class DocumentsController(
    AdminDbContext db,
    IUserPermissionsRepository permissions,
    IDocumentEmailSender documentEmailSender,
    ILogger<DocumentsController> logger) : ControllerBase
{
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    /// <summary>Lists documents, optionally filtered by type/project and by active status.</summary>
    [HttpGet]
    public async Task<IActionResult> GetDocumentsAsync(
        [FromQuery] DocumentType? type, [FromQuery] Guid? projectId, [FromQuery] bool activeOnly = true)
    {
        var query = db.Documents.AsQueryable();

        if (type is not null)
            query = query.Where(d => d.Type == type);

        query = query.Where(d => d.ProjectId == projectId);

        if (activeOnly)
            query = query.Where(d => d.IsActive);

        var documents = await query
            .Select(d => new DocumentResponse(d.Id, d.Type, d.Title, d.Version, d.Content, d.PublishedAt, d.EffectiveAt, d.IsActive, d.IsRequired, d.ProjectId))
            .ToListAsync();

        return Ok(documents);
    }

    /// <summary>Gets a single document by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDocumentAsync(Guid id)
    {
        var document = await db.Documents.FindAsync(id);
        if (document is null)
            return NotFound();

        return Ok(new DocumentResponse(
            document.Id, document.Type, document.Title, document.Version, document.Content, document.PublishedAt, document.EffectiveAt, document.IsActive, document.IsRequired, document.ProjectId));
    }

    /// <summary>Renders a document's Markdown content as a standalone HTML page (e.g. opened from a consent checkbox).</summary>
    [HttpGet("{id:guid}/view")]
    public async Task<IActionResult> ViewDocumentAsync(Guid id)
    {
        var document = await db.Documents.FindAsync(id);
        if (document is null)
            return NotFound();

        var title = WebUtility.HtmlEncode(document.Title);
        var contentHtml = Markdown.ToHtml(document.Content, MarkdownPipeline);
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        var html = $"""
            <!doctype html>
            <html lang="{lang}">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>{title} — Strnadi</title>
              <link rel="stylesheet" href="/design-system.css" />
            </head>
            <body style="background:var(--color-bg);margin:0;padding:var(--space-8) var(--space-4);">
              <article class="ss-card ss-prose" style="max-width:720px;margin:0 auto;padding:var(--space-6);">
                <h1>{title}</h1>
                {contentHtml}
              </article>
            </body>
            </html>
            """;

        return Content(html, "text/html");
    }

    /// <summary>Creates the first version of a document for a type/project.</summary>
    [Authorize(Policy = "AccountMutation")]
    [HttpPost]
    public async Task<IActionResult> CreateDocumentAsync([FromBody] CreateDocumentRequest request)
    {
        if (!await permissions.HasPermissionAsync(GetCurrentUserId(), Permissions.ManageDocuments))
            return Forbid();

        var exists = await db.Documents.AnyAsync(d =>
            d.Type == request.Type && d.ProjectId == request.ProjectId && d.IsActive);

        if (exists)
            return Conflict($"An active document of type {request.Type} already exists for this project.");

        var publishedAt = DateTime.UtcNow;

        var document = new Document
        {
            Id = Guid.NewGuid(),
            Type = request.Type,
            Title = request.Title,
            Version = 1,
            Content = request.Content,
            PublishedAt = publishedAt,
            EffectiveAt = request.EffectiveAt ?? publishedAt,
            IsActive = true,
            IsRequired = request.IsRequired,
            ProjectId = request.ProjectId
        };

        db.Documents.Add(document);
        await db.SaveChangesAsync();

        logger.LogInformation("Created document {DocumentId} of type {Type} for project {ProjectId}, effective {EffectiveAt}",
            document.Id, document.Type, document.ProjectId, document.EffectiveAt);

        // MvcOptions.SuppressAsyncSuffixInActionNames defaults to true, so the action is routable
        // as "GetDocument", not "GetDocumentAsync" - see ProjectMembersController.JoinAsync for
        // the full explanation of why nameof() alone breaks this.
        return CreatedAtAction(nameof(GetDocumentAsync)[..^"Async".Length], new { id = document.Id }, new DocumentResponse(
            document.Id, document.Type, document.Title, document.Version, document.Content, document.PublishedAt, document.EffectiveAt, document.IsActive, document.IsRequired, document.ProjectId));
    }

    /// <summary>Publishes a new version of a document: deactivates the current one and creates the next version.</summary>
    [Authorize(Policy = "AccountMutation")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateDocumentAsync(Guid id, [FromBody] UpdateDocumentRequest request)
    {
        if (!await permissions.HasPermissionAsync(GetCurrentUserId(), Permissions.ManageDocuments))
            return Forbid();

        var current = await db.Documents.FindAsync(id);
        if (current is null)
            return NotFound();

        if (!current.IsActive)
            return Conflict("Only the active version of a document can be superseded.");

        current.IsActive = false;

        var publishedAt = DateTime.UtcNow;

        var next = new Document
        {
            Id = Guid.NewGuid(),
            Type = current.Type,
            Title = request.Title ?? current.Title,
            Version = current.Version + 1,
            Content = request.Content,
            PublishedAt = publishedAt,
            EffectiveAt = request.EffectiveAt ?? publishedAt,
            IsActive = true,
            IsRequired = request.IsRequired ?? current.IsRequired,
            ProjectId = current.ProjectId
        };

        db.Documents.Add(next);
        await db.SaveChangesAsync();

        logger.LogInformation("Published document {DocumentId} as version {Version} (supersedes {PreviousId}), effective {EffectiveAt}",
            next.Id, next.Version, current.Id, next.EffectiveAt);

        await NotifyAffectedUsersAsync(current, next);

        return CreatedAtAction(nameof(GetDocumentAsync)[..^"Async".Length], new { id = next.Id }, new DocumentResponse(
            next.Id, next.Type, next.Title, next.Version, next.Content, next.PublishedAt, next.EffectiveAt, next.IsActive, next.IsRequired, next.ProjectId));
    }

    /// <summary>Emails everyone who had accepted the previous version, since that acceptance no longer covers the new one.</summary>
    private async Task NotifyAffectedUsersAsync(Document previousVersion, Document newVersion)
    {
        var affectedUsers = await db.DocumentAcceptances
            .Where(da => da.DocumentId == previousVersion.Id && da.RevokedAt == null)
            .Join(db.Users, da => da.UserId, u => u.Id, (_, u) => u)
            .Where(u => u.Email != null)
            .ToListAsync();

        if (affectedUsers.Count == 0)
            return;

        var documentLink = $"{Request.Scheme}://{Request.Host}/dashboard";

        foreach (var user in affectedUsers)
            await documentEmailSender.SendDocumentUpdatedAsync(user, user.Email!, newVersion, documentLink);

        logger.LogInformation("Notified {UserCount} users about the new version of document {DocumentId}",
            affectedUsers.Count, newVersion.Id);
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirstValue(OpenIddictConstants.Claims.Subject) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(userId!);
    }
}
