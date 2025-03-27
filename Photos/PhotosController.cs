using Auth.Services;
using Microsoft.AspNetCore.Mvc;
using Repository;
using Shared.Extensions;
using Shared.Logging;
using Shared.Models.Requests.Photos;

namespace Photos;

[ApiController]
[Route("photos")]
public class PhotosController : ControllerBase
{
    [HttpPost("recording-photo")]
    public async Task<IActionResult> UploadPhoto([FromBody] UploadRecordingPhotoRequest request,
        [FromServices] PhotosRepository repo,
        [FromServices] JwtService jwtService)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        bool success = await repo.UploadAsync(request);
        if (success) Logger.Log($"Photo for recording {request.RecordingId} has been uploaded");
        
        return success ? Ok() : Conflict("Failed to save recording photo");
    }
}