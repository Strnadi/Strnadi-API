using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Strnadi.Application.Recordings;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("recordings/filtered")]
public class FilteredRecordingsController(FilteredRecordingPartsService filteredRecordingPartsService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync([FromQuery] int? recordingId, [FromQuery] bool verified, CancellationToken cancellationToken)
    {
        return Ok(await filteredRecordingPartsService.GetAllAsync(recordingId, verified, cancellationToken));
    }

    [HttpGet("{fpId:int}")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] int fpId, CancellationToken cancellationToken)
    {
        return Ok(await filteredRecordingPartsService.GetByIdAsync(fpId, cancellationToken));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> UploadAsync([FromBody] FilteredRecordingPartUploadRequest request, CancellationToken cancellationToken)
    {
        return Ok(await filteredRecordingPartsService.UploadAsync(request, cancellationToken));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("post-confirmed-dialect")]
    public async Task<IActionResult> PostConfirmedDialectAsync([FromBody] PostConfirmedDialectRequest request, CancellationToken cancellationToken)
    {
        return Ok(await filteredRecordingPartsService.PostConfirmedDialectAsync(request, cancellationToken));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPatch("update-confirmed-dialect")]
    public async Task<IActionResult> UpdateConfirmedDialectAsync([FromBody] UpdateConfirmedDialectRequest request, CancellationToken cancellationToken)
    {
        await filteredRecordingPartsService.UpdateConfirmedDialectAsync(request, cancellationToken);
        return Ok();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPatch("{fpId:int}")]
    public async Task<IActionResult> UpdateAsync([FromRoute] int fpId, [FromBody] FilteredRecordingPartUpdateRequest request, CancellationToken cancellationToken)
    {
        await filteredRecordingPartsService.UpdateAsync(fpId, request, cancellationToken);
        return Ok();
    }

    [Obsolete("use {fpId} DELETE instead")]
    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("delete-confirmed-dialect/{filteredPartId:int}")]
    public async Task<IActionResult> DeleteConfirmedDialectAsync([FromRoute] int filteredPartId, CancellationToken cancellationToken)
    {
        await filteredRecordingPartsService.DeleteConfirmedDialectAsync(filteredPartId, cancellationToken);
        return Ok();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{fpId:int}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] int fpId, CancellationToken cancellationToken)
    {
        await filteredRecordingPartsService.DeleteAsync(fpId, cancellationToken);
        return Ok();
    }
}
