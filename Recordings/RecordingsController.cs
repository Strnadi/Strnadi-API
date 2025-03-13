using Auth.Services;
using Microsoft.AspNetCore.Http;
using Repository;
using Microsoft.AspNetCore.Mvc;
using Shared.Extensions;
using Shared.Models.Requests;

namespace Recordings;

[ApiController]
[Route("recordings")]
public class RecordingsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRecordings([FromServices] RecordingsRepository repo,
        [FromQuery] string? email = null,
        [FromQuery] bool parts = false,
        [FromQuery] bool sound = false)
    {
        var recordings = await repo.GetAsync(email, parts, sound);
        return Ok(recordings);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetRecording(int id,
        [FromServices] RecordingsRepository repo,
        [FromQuery] bool parts = false,
        [FromQuery] bool sound = false)
    {
        var recording = await repo.GetAsync(id, parts, sound);
        
        if (recording is null)
            return NotFound("Recording with provided ID not found");
        
        return Ok(recording);
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromBody] RecordingUploadModel model,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo,
        [FromServices] RecordingsRepository recordingsRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();
        
        int? recordingId = await recordingsRepo.UploadAsync(email!, model);
        
        if (recordingId is null)
            return StatusCode(500, "Failed to upload recording");

        return Ok(recordingId);
    }

    [HttpPost("upload-part")]
    public async Task<IActionResult> UploadPart([FromBody] RecordingPartUploadModel model,
        [FromServices] JwtService jwtService,
        [FromServices] RecordingsRepository recordingsRepo)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null) 
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out _))
            return Unauthorized();
        
        int? recordingPartId = await recordingsRepo.UploadPartAsync(model);
        
        if (recordingPartId is null)
            return StatusCode(500, "Failed to upload recording");

        return Ok(recordingPartId);
    }
}