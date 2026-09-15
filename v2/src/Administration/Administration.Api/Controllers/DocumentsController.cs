using System.Security.Claims;
using Administration.Application.Documents;
using Administration.Domain.Entities;
using Administration.Domain.Entities.Enums;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
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
    ILogger<DocumentsController> logger) : ControllerBase
{
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
            .Select(d => new DocumentResponse(d.Id, d.Type, d.Version, d.Content, d.PublishedAt, d.EffectiveAt, d.IsActive, d.ProjectId))
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
            document.Id, document.Type, document.Version, document.Content, document.PublishedAt, document.EffectiveAt, document.IsActive, document.ProjectId));
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
            Version = 1,
            Content = request.Content,
            PublishedAt = publishedAt,
            EffectiveAt = request.EffectiveAt ?? publishedAt,
            IsActive = true,
            ProjectId = request.ProjectId
        };

        db.Documents.Add(document);
        await db.SaveChangesAsync();

        logger.LogInformation("Created document {DocumentId} of type {Type} for project {ProjectId}, effective {EffectiveAt}",
            document.Id, document.Type, document.ProjectId, document.EffectiveAt);

        return CreatedAtAction(nameof(GetDocumentAsync), new { id = document.Id }, new DocumentResponse(
            document.Id, document.Type, document.Version, document.Content, document.PublishedAt, document.EffectiveAt, document.IsActive, document.ProjectId));
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
            Version = current.Version + 1,
            Content = request.Content,
            PublishedAt = publishedAt,
            EffectiveAt = request.EffectiveAt ?? publishedAt,
            IsActive = true,
            ProjectId = current.ProjectId
        };

        db.Documents.Add(next);
        await db.SaveChangesAsync();

        logger.LogInformation("Published document {DocumentId} as version {Version} (supersedes {PreviousId}), effective {EffectiveAt}",
            next.Id, next.Version, current.Id, next.EffectiveAt);

        return CreatedAtAction(nameof(GetDocumentAsync), new { id = next.Id }, new DocumentResponse(
            next.Id, next.Type, next.Version, next.Content, next.PublishedAt, next.EffectiveAt, next.IsActive, next.ProjectId));
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirstValue(OpenIddictConstants.Claims.Subject) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(userId!);
    }
}
