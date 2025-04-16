using Auth.Services;
using Microsoft.AspNetCore.Mvc;
using Repository;
using Shared.Extensions;
using Shared.Logging;
using Shared.Models.Requests.Recordings;

namespace Recordings;

[ApiController]
[Route("filtered")]
public class FilteredRecordingsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetFilteredPartsAsync([FromQuery] int recordingPartId,
        [FromServices] RecordingsRepository recordingsRepo,
        [FromQuery] bool verified = false)
    {
        var filtered = await recordingsRepo.GetFilteredPartsAsync(recordingPartId, verified);
        
        if (filtered is null) 
            return StatusCode(409, "Failed to get filtered parts");

        if (filtered.Length is 0)
            return NoContent();

        return Ok(filtered);
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFilteredPartAsync(FilteredRecordingPartUploadRequest model,
        [FromServices] JwtService jwtService,
        [FromServices] RecordingsRepository recordingsRepo)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        bool added = await recordingsRepo.UploadFilteredPartAsync(model);

        Logger.Log(added
            ? $"Filtered part for recording {model.RecordingId} has been uploaded"
            : $"Failed to upload filtered part for recording {model.RecordingId}");

        return added ? 
            Ok() :
            Conflict();
    }
}