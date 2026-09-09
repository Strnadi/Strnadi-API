using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tenant.Api.Extensions;
using Tenant.Application.Photos;

namespace Tenant.Api.Controllers;

[ApiController]
[Route("recordings/{recordingId:int}/photos")]
public class RecordingPhotosController(RecordingPhotosService photos) : ControllerBase
{
    /// <summary>All photos attached to a recording.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(RecordingPhotoModel[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync([FromRoute] int recordingId, CancellationToken cancellationToken)
    {
        return Ok(await photos.GetAllAsync(recordingId, cancellationToken));
    }

    /// <summary>Attaches a new photo to a recording. Only the recording's owner (or an admin) may do this.</summary>
    [Authorize]
    [HttpPost]
    [RequestSizeLimit(130023424)]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadAsync([FromRoute] int recordingId, [FromBody] UploadRecordingPhotoRequest request, CancellationToken cancellationToken)
    {
        var id = await photos.UploadAsync(recordingId, request, this.GetCallerId(), this.IsAdmin(), cancellationToken);
        return Ok(id);
    }

    /// <summary>Deletes a photo from a recording. Only the recording's owner (or an admin) may do this.</summary>
    [Authorize]
    [HttpDelete("{photoId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync([FromRoute] int recordingId, [FromRoute] int photoId, CancellationToken cancellationToken)
    {
        await photos.DeleteAsync(recordingId, photoId, this.GetCallerId(), this.IsAdmin(), cancellationToken);
        return Ok();
    }
}
