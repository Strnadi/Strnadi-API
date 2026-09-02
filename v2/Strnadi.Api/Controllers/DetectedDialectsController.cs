using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Strnadi.Application.Recordings;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("recordings/filtered/detected")]
public class DetectedDialectsController(DetectedDialectsService detectedDialectsService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        return Ok(await detectedDialectsService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{ddId:int}")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] int ddId, CancellationToken cancellationToken)
    {
        return Ok(await detectedDialectsService.GetByIdAsync(ddId, cancellationToken));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] DetectedDialectUploadRequest request, CancellationToken cancellationToken)
    {
        var id = await detectedDialectsService.CreateAsync(request, cancellationToken);
        return Created($"detected/{id}", id);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPatch]
    public async Task<IActionResult> UpdateAsync([FromBody] UpdateDetectedDialectRequest request, CancellationToken cancellationToken)
    {
        await detectedDialectsService.UpdateAsync(request, cancellationToken);
        return Ok();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{ddId:int}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] int ddId, CancellationToken cancellationToken)
    {
        await detectedDialectsService.DeleteAsync(ddId, cancellationToken);
        return Ok();
    }
}
