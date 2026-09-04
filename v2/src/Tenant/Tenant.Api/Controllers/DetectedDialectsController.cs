using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tenant.Application.Recordings;
using Tenant.Domain.Entities;

namespace Tenant.Api.Controllers;

[ApiController]
[Route("recordings/filtered/detected")]
public class DetectedDialectsController(DetectedDialectsService detectedDialectsService) : ControllerBase
{
    /// <summary>All detected dialects.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(DetectedDialect[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        return Ok(await detectedDialectsService.GetAllAsync(cancellationToken));
    }

    /// <summary>A detected dialect by id.</summary>
    [HttpGet("{ddId:int}")]
    [ProducesResponseType(typeof(DetectedDialect), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync([FromRoute] int ddId, CancellationToken cancellationToken)
    {
        return Ok(await detectedDialectsService.GetByIdAsync(ddId, cancellationToken));
    }

    /// <summary>Records a detected dialect for a filtered recording part. Admin only.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAsync([FromBody] DetectedDialectUploadRequest request, CancellationToken cancellationToken)
    {
        var id = await detectedDialectsService.CreateAsync(request, cancellationToken);
        return Created($"detected/{id}", id);
    }

    /// <summary>Updates a detected dialect. Admin only.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpPatch]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync([FromBody] UpdateDetectedDialectRequest request, CancellationToken cancellationToken)
    {
        await detectedDialectsService.UpdateAsync(request, cancellationToken);
        return Ok();
    }

    /// <summary>Deletes a detected dialect. Admin only.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{ddId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync([FromRoute] int ddId, CancellationToken cancellationToken)
    {
        await detectedDialectsService.DeleteAsync(ddId, cancellationToken);
        return Ok();
    }
}
