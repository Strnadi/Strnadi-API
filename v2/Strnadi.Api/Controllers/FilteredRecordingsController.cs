using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Strnadi.Application.Recordings;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("recordings/filtered")]
public class FilteredRecordingsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync([FromQuery] int? recordingId, [FromQuery] bool verified, CancellationToken cancellationToken)
    {
    }

    [HttpGet("{fpId:int}")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] int fpId, CancellationToken cancellationToken)
    {
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> UploadAsync([FromBody] FilteredRecordingPartUploadRequest request, CancellationToken cancellationToken)
    {
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("post-confirmed-dialect")]
    public async Task<IActionResult> PostConfirmedDialectAsync([FromBody] PostConfirmedDialectRequest request, CancellationToken cancellationToken)
    {
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPatch("update-confirmed-dialect")]
    public async Task<IActionResult> UpdateConfirmedDialectAsync([FromBody] UpdateConfirmedDialectRequest request, CancellationToken cancellationToken)
    {
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPatch("{fpId:int}")]
    public async Task<IActionResult> UpdateAsync([FromRoute] int fpId, [FromBody] FilteredRecordingPartUpdateRequest request, CancellationToken cancellationToken)
    {
    }

    [Obsolete("use {fpId} DELETE instead")]
    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("delete-confirmed-dialect/{filteredPartId:int}")]
    public async Task<IActionResult> DeleteConfirmedDialectAsync([FromRoute] int filteredPartId, CancellationToken cancellationToken)
    {
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{fpId:int}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] int fpId, CancellationToken cancellationToken)
    {
    }
}
