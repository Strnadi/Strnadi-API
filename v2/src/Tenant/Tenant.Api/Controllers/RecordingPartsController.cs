using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Platform.Shared.Kernel.Authorization;
using Tenant.Api.Extensions;
using Tenant.Application.Recordings;

namespace Tenant.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/recordings")]
public class RecordingPartsController(RecordingPartsService recordingPartsService) : ControllerBase
{
    /// <summary>Kept for old clients still hitting the three-segment route; same as <see cref="GetSoundAsync"/>.</summary>
    [Obsolete("use part/{partId:int}/sound GET instead")]
    [Authorize]
    [HttpGet("part/{recId:int}/{partId:int}/sound")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "audio/wav")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status206PartialContent, "audio/wav")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSoundLegacyAsync([FromRoute] int recId, [FromRoute] int partId, CancellationToken cancellationToken)
    {
        var bytes = await recordingPartsService.GetSoundAsync(
            partId, this.GetCallerId(), this.HasPermission(Permissions.DownloadRecordings), cancellationToken);
        return File(bytes, "audio/wav", enableRangeProcessing: true);
    }

    /// <summary>The audio for a recording part, seekable via range requests. Requires ownership of the
    /// recording, or the <see cref="Permissions.DownloadRecordings"/> permission to download others'.</summary>
    [Authorize]
    [HttpGet("part/{partId:int}/sound")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "audio/wav")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status206PartialContent, "audio/wav")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSoundAsync([FromRoute] int partId, CancellationToken cancellationToken)
    {
        var bytes = await recordingPartsService.GetSoundAsync(
            partId, this.GetCallerId(), this.HasPermission(Permissions.DownloadRecordings), cancellationToken);
        return File(bytes, "audio/wav", enableRangeProcessing: true);
    }

    /// <summary>Uploads a recording part's metadata; the audio follows separately.</summary>
    [Authorize]
    [HttpPost("part")]
    [RequestSizeLimit(int.MaxValue)]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadPartAsync([FromBody] RecordingPartUploadRequest request, CancellationToken cancellationToken)
    {
        return Ok(await recordingPartsService.UploadPartAsync(request, cancellationToken));
    }

    /// <summary>Uploads a recording part together with its audio file.</summary>
    [Authorize]
    [HttpPost("part-new")]
    [RequestSizeLimit(int.MaxValue)]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadPartWithFileAsync([FromForm] RecordingPartUploadRequest request, IFormFile file, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);

        return Ok(await recordingPartsService.UploadPartWithFileAsync(request, stream.ToArray(), cancellationToken));
    }
}
