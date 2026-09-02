using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Strnadi.Api.Extensions;
using Strnadi.Application.Recordings;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("recordings")]
public class RecordingsController(RecordingsService recordingsService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync([FromQuery] int? userId, [FromQuery] bool parts, [FromQuery] bool sound, CancellationToken cancellationToken)
    {
        return Ok(await recordingsService.GetAllAsync(userId, parts, sound, cancellationToken));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("deleted")]
    public async Task<IActionResult> GetDeletedAsync(CancellationToken cancellationToken)
    {
        return Ok(await recordingsService.GetDeletedAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] int id, [FromQuery] bool parts, [FromQuery] bool sound, CancellationToken cancellationToken)
    {
        return Ok(await recordingsService.GetByIdAsync(id, parts, sound, cancellationToken));
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] int id, [FromQuery] bool final, CancellationToken cancellationToken)
    {
        await recordingsService.DeleteAsync(id, final, this.GetCallerId(), this.IsAdmin(), cancellationToken);
        return Ok();
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> UploadAsync([FromBody] RecordingUploadRequest request, CancellationToken cancellationToken)
    {
        return Ok(await recordingsService.CreateAsync(request, this.GetCallerId(), cancellationToken));
    }

    [Authorize]
    [HttpGet("incomplete")]
    public async Task<IActionResult> GetIncompleteAsync(CancellationToken cancellationToken)
    {
        return Ok(await recordingsService.GetIncompleteAsync(this.GetCallerId(), cancellationToken));
    }

    [Authorize]
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateRecordingRequest request, CancellationToken cancellationToken)
    {
        await recordingsService.UpdateAsync(id, request, this.GetCallerId(), this.IsAdmin(), cancellationToken);
        return Ok();
    }

    [HttpGet("dialects")]
    public async Task<IActionResult> GetDialectsAsync(CancellationToken cancellationToken)
    {
        return Ok(await recordingsService.GetDialectsAsync(cancellationToken));
    }
}
