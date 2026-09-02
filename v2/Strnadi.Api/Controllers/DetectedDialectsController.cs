using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Strnadi.Application.Recordings;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("recordings/filtered/detected")]
public class DetectedDialectsController(DetectedDialectsService detectedDialectsService) : ControllerBase
{
    /// <summary>All detected dialects.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        return Ok(await detectedDialectsService.GetAllAsync(cancellationToken));
    }

    /// <summary>A detected dialect by id.</summary>
    [HttpGet("{ddId:int}")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] int ddId, CancellationToken cancellationToken)
    {
        return Ok(await detectedDialectsService.GetByIdAsync(ddId, cancellationToken));
    }

    /// <summary>Records a detected dialect for a filtered recording part. Admin only.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] DetectedDialectUploadRequest request, CancellationToken cancellationToken)
    {
        var id = await detectedDialectsService.CreateAsync(request, cancellationToken);
        return Created($"detected/{id}", id);
    }

    /// <summary>Updates a detected dialect. Admin only.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpPatch]
    public async Task<IActionResult> UpdateAsync([FromBody] UpdateDetectedDialectRequest request, CancellationToken cancellationToken)
    {
        await detectedDialectsService.UpdateAsync(request, cancellationToken);
        return Ok();
    }

    /// <summary>Deletes a detected dialect. Admin only.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{ddId:int}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] int ddId, CancellationToken cancellationToken)
    {
        await detectedDialectsService.DeleteAsync(ddId, cancellationToken);
        return Ok();
    }
}
