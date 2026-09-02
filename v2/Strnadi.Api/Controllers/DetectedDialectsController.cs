using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Strnadi.Application.Recordings;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("recordings/filtered/detected")]
public class DetectedDialectsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
    }

    [HttpGet("{ddId:int}")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] int ddId, CancellationToken cancellationToken)
    {
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] DetectedDialectUploadRequest request, CancellationToken cancellationToken)
    {
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPatch]
    public async Task<IActionResult> UpdateAsync([FromBody] UpdateDetectedDialectRequest request, CancellationToken cancellationToken)
    {
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{ddId:int}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] int ddId, CancellationToken cancellationToken)
    {
    }
}
