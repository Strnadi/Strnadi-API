using System.Security.Claims;
using Administration.Application.Documents;
using Administration.Domain.Entities;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Administration.Api.Controllers;

[ApiController]
[Authorize(Policy = "AccountMutation")]
[Route("document-acceptances")]
public class DocumentAcceptancesController(AdminDbContext db, ILogger<DocumentAcceptancesController> logger) : ControllerBase
{
    /// <summary>Lists the caller's own document acceptances.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAcceptancesAsync()
    {
        var userId = GetCurrentUserId();

        var acceptances = await db.DocumentAcceptances
            .Where(da => da.UserId == userId)
            .Select(da => new DocumentAcceptanceResponse(da.Id, da.UserId, da.DocumentId, da.AcceptedAt, da.RevokedAt))
            .ToListAsync();

        return Ok(acceptances);
    }

    /// <summary>Gets one of the caller's own document acceptances.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAcceptanceAsync(Guid id)
    {
        var userId = GetCurrentUserId();

        var acceptance = await db.DocumentAcceptances.FindAsync(id);
        if (acceptance is null || acceptance.UserId != userId)
            return NotFound();

        return Ok(new DocumentAcceptanceResponse(
            acceptance.Id, acceptance.UserId, acceptance.DocumentId, acceptance.AcceptedAt, acceptance.RevokedAt));
    }

    /// <summary>Records the caller's acceptance of a document.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAcceptanceAsync([FromBody] CreateDocumentAcceptanceRequest request)
    {
        var userId = GetCurrentUserId();

        var document = await db.Documents.FindAsync(request.DocumentId);
        if (document is null)
            return NotFound("Document not found.");

        var alreadyAccepted = await db.DocumentAcceptances.AnyAsync(da =>
            da.UserId == userId && da.DocumentId == request.DocumentId && da.RevokedAt == null);

        if (alreadyAccepted)
            return Conflict("This document has already been accepted.");

        var acceptance = new DocumentAcceptance
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DocumentId = request.DocumentId,
            AcceptedAt = DateTime.UtcNow,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        db.DocumentAcceptances.Add(acceptance);
        await db.SaveChangesAsync();

        logger.LogInformation("User {UserId} accepted document {DocumentId}", userId, request.DocumentId);

        return CreatedAtAction(nameof(GetAcceptanceAsync), new { id = acceptance.Id }, new DocumentAcceptanceResponse(
            acceptance.Id, acceptance.UserId, acceptance.DocumentId, acceptance.AcceptedAt, acceptance.RevokedAt));
    }

    /// <summary>Revokes one of the caller's own document acceptances (soft delete via RevokedAt).</summary>
    [HttpPatch("{id:guid}/revoke")]
    public async Task<IActionResult> RevokeAcceptanceAsync(Guid id)
    {
        var userId = GetCurrentUserId();

        var acceptance = await db.DocumentAcceptances.FindAsync(id);
        if (acceptance is null || acceptance.UserId != userId)
            return NotFound();

        if (acceptance.RevokedAt is not null)
            return Conflict("This acceptance has already been revoked.");

        acceptance.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("User {UserId} revoked acceptance {AcceptanceId} of document {DocumentId}",
            userId, acceptance.Id, acceptance.DocumentId);

        return Ok(new DocumentAcceptanceResponse(
            acceptance.Id, acceptance.UserId, acceptance.DocumentId, acceptance.AcceptedAt, acceptance.RevokedAt));
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirstValue(Claims.Subject) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(userId!);
    }
}
