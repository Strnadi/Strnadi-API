using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tenant.Api.Extensions;
using Tenant.Application.Recordings;

namespace Tenant.Api.Controllers;

[ApiController]
[Route("recordings")]
public class RecordingsController(RecordingsService recordingsService) : ControllerBase
{
    /// <summary>Recordings, optionally with their parts and/or audio.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAllAsync([FromQuery] int? userId, [FromQuery] bool parts, [FromQuery] bool sound, CancellationToken cancellationToken)
    {
        return Ok(await recordingsService.GetAllAsync(userId, parts, sound, cancellationToken));
    }

    /// <summary>Soft-deleted recordings. Admin only.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpGet("deleted")]
    public async Task<IActionResult> GetDeletedAsync(CancellationToken cancellationToken)
    {
        return Ok(await recordingsService.GetDeletedAsync(cancellationToken));
    }

    /// <summary>A recording by id, optionally with its parts and/or audio.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] int id, [FromQuery] bool parts, [FromQuery] bool sound, CancellationToken cancellationToken)
    {
        return Ok(await recordingsService.GetByIdAsync(id, parts, sound, cancellationToken));
    }

    /// <summary>Deletes a recording; soft by default, permanently when <paramref name="final"/> is set.</summary>
    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] int id, [FromQuery] bool final, CancellationToken cancellationToken)
    {
        await recordingsService.DeleteAsync(id, final, this.GetCallerId(), this.IsAdmin(), cancellationToken);
        return Ok();
    }

    /// <summary>Creates a recording.</summary>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> UploadAsync([FromBody] RecordingUploadRequest request, CancellationToken cancellationToken)
    {
        return Ok(await recordingsService.CreateAsync(request, this.GetCallerId(), cancellationToken));
    }

    /// <summary>The caller's recordings that are still missing parts.</summary>
    [Authorize]
    [HttpGet("incomplete")]
    public async Task<IActionResult> GetIncompleteAsync(CancellationToken cancellationToken)
    {
        return Ok(await recordingsService.GetIncompleteAsync(this.GetCallerId(), cancellationToken));
    }

    /// <summary>Updates a recording.</summary>
    [Authorize]
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateRecordingRequest request, CancellationToken cancellationToken)
    {
        await recordingsService.UpdateAsync(id, request, this.GetCallerId(), this.IsAdmin(), cancellationToken);
        return Ok();
    }

    /// <summary>All known bird dialects.</summary>
    [HttpGet("dialects")]
    public async Task<IActionResult> GetDialectsAsync(CancellationToken cancellationToken)
    {
        return Ok(await recordingsService.GetDialectsAsync(cancellationToken));
    }
}
