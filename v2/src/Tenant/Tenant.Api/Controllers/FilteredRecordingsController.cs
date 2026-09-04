using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tenant.Application.Recordings;
using Tenant.Domain.Entities;

namespace Tenant.Api.Controllers;

[ApiController]
[Route("recordings/filtered")]
public class FilteredRecordingsController(FilteredRecordingPartsService filteredRecordingPartsService) : ControllerBase
{
    /// <summary>Filtered recording parts, optionally scoped to a recording or only the verified ones.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(FilteredRecordingPart[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync([FromQuery] int? recordingId, [FromQuery] bool verified, CancellationToken cancellationToken)
    {
        return Ok(await filteredRecordingPartsService.GetAllAsync(recordingId, verified, cancellationToken));
    }

    /// <summary>A filtered recording part by id.</summary>
    [HttpGet("{fpId:int}")]
    [ProducesResponseType(typeof(FilteredRecordingPart), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync([FromRoute] int fpId, CancellationToken cancellationToken)
    {
        return Ok(await filteredRecordingPartsService.GetByIdAsync(fpId, cancellationToken));
    }

    /// <summary>Submits a filtered recording part for review.</summary>
    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadAsync([FromBody] FilteredRecordingPartUploadRequest request, CancellationToken cancellationToken)
    {
        return Ok(await filteredRecordingPartsService.UploadAsync(request, cancellationToken));
    }

    /// <summary>Confirms a dialect for a filtered part. Admin only.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("post-confirmed-dialect")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostConfirmedDialectAsync([FromBody] PostConfirmedDialectRequest request, CancellationToken cancellationToken)
    {
        return Ok(await filteredRecordingPartsService.PostConfirmedDialectAsync(request, cancellationToken));
    }

    /// <summary>Updates a confirmed dialect. Admin only.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpPatch("update-confirmed-dialect")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateConfirmedDialectAsync([FromBody] UpdateConfirmedDialectRequest request, CancellationToken cancellationToken)
    {
        await filteredRecordingPartsService.UpdateConfirmedDialectAsync(request, cancellationToken);
        return Ok();
    }

    /// <summary>Updates a filtered recording part. Admin only.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpPatch("{fpId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync([FromRoute] int fpId, [FromBody] FilteredRecordingPartUpdateRequest request, CancellationToken cancellationToken)
    {
        await filteredRecordingPartsService.UpdateAsync(fpId, request, cancellationToken);
        return Ok();
    }

    /// <summary>Superseded by <see cref="DeleteAsync"/>; kept for old clients.</summary>
    [Obsolete("use {fpId} DELETE instead")]
    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("delete-confirmed-dialect/{filteredPartId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConfirmedDialectAsync([FromRoute] int filteredPartId, CancellationToken cancellationToken)
    {
        await filteredRecordingPartsService.DeleteConfirmedDialectAsync(filteredPartId, cancellationToken);
        return Ok();
    }

    /// <summary>Deletes a filtered recording part. Admin only.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{fpId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync([FromRoute] int fpId, CancellationToken cancellationToken)
    {
        await filteredRecordingPartsService.DeleteAsync(fpId, cancellationToken);
        return Ok();
    }
}
