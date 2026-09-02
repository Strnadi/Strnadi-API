using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Strnadi.Application.Recordings;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("recordings")]
public class RecordingPartsController(RecordingPartsService recordingPartsService) : ControllerBase
{
    /// <summary>Kept for old clients still hitting the three-segment route; same as <see cref="GetSoundAsync"/>.</summary>
    [Obsolete("use part/{partId:int}/sound GET instead")]
    [HttpGet("part/{recId:int}/{partId:int}/sound")]
    public async Task<IActionResult> GetSoundLegacyAsync([FromRoute] int recId, [FromRoute] int partId, CancellationToken cancellationToken)
    {
        var bytes = await recordingPartsService.GetSoundAsync(partId, cancellationToken);
        return File(bytes, "audio/wav", enableRangeProcessing: true);
    }

    /// <summary>The audio for a recording part, seekable via range requests.</summary>
    [HttpGet("part/{partId:int}/sound")]
    public async Task<IActionResult> GetSoundAsync([FromRoute] int partId, CancellationToken cancellationToken)
    {
        var bytes = await recordingPartsService.GetSoundAsync(partId, cancellationToken);
        return File(bytes, "audio/wav", enableRangeProcessing: true);
    }

    /// <summary>Uploads a recording part's metadata; the audio follows separately.</summary>
    [Authorize]
    [HttpPost("part")]
    [RequestSizeLimit(int.MaxValue)]
    public async Task<IActionResult> UploadPartAsync([FromBody] RecordingPartUploadRequest request, CancellationToken cancellationToken)
    {
        return Ok(await recordingPartsService.UploadPartAsync(request, cancellationToken));
    }

    /// <summary>Uploads a recording part together with its audio file.</summary>
    [Authorize]
    [HttpPost("part-new")]
    [RequestSizeLimit(int.MaxValue)]
    public async Task<IActionResult> UploadPartWithFileAsync([FromForm] RecordingPartUploadRequest request, IFormFile file, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);

        return Ok(await recordingPartsService.UploadPartWithFileAsync(request, stream.ToArray(), cancellationToken));
    }
}
