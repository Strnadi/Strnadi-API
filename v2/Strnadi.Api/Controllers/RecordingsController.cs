using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Strnadi.Api.Extensions;
using Strnadi.Application.Recordings;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("recordings")]
public class RecordingsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync([FromQuery] int? userId, [FromQuery] bool parts, [FromQuery] bool sound, CancellationToken cancellationToken)
    {
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("deleted")]
    public async Task<IActionResult> GetDeletedAsync(CancellationToken cancellationToken)
    {
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] int id, [FromQuery] bool parts, [FromQuery] bool sound, CancellationToken cancellationToken)
    {
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] int id, [FromQuery] bool final, CancellationToken cancellationToken)
    {
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> UploadAsync([FromBody] RecordingUploadRequest request, CancellationToken cancellationToken)
    {
    }

    [Authorize]
    [HttpGet("incomplete")]
    public async Task<IActionResult> GetIncompleteAsync(CancellationToken cancellationToken)
    {
    }

    [Authorize]
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateRecordingRequest request, CancellationToken cancellationToken)
    {
    }

    [HttpGet("dialects")]
    public async Task<IActionResult> GetDialectsAsync(CancellationToken cancellationToken)
    {
    }
}
