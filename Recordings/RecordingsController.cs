/*
 * Copyright (C) 2024 Stanislav Motsnyi
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using Auth.Services;
using Repository;
using Microsoft.AspNetCore.Mvc;
using Shared.Extensions;
using Shared.Logging;
using Shared.Models.Requests.Recordings;

namespace Recordings;

[ApiController]
[Route("recordings")]
public class RecordingsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRecordingsAsync([FromServices] RecordingsRepository repo,
        [FromQuery] int? userId = null,
        [FromQuery] bool parts = false,
        [FromQuery] bool sound = false)
    {
        var recordings = await repo.GetAsync(userId, parts, sound);
        
        if (recordings is null)
            return StatusCode(500, "Failed to get recordings");

        if (recordings.Length is 0)
            return NoContent();
        
        return Ok(recordings);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetRecordingAsync(int id,
        [FromServices] RecordingsRepository repo,
        [FromQuery] bool parts = false,
        [FromQuery] bool sound = false)
    {
        var recording = await repo.GetAsync(id, parts, sound);
        
        if (recording is null)
            return NoContent();
        
        return Ok(recording);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteRecordingAsync([FromRoute] int id,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo,
        [FromServices] RecordingsRepository recordingsRepo,
        [FromQuery] bool final = false)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();
        
        if (!await recordingsRepo.ExistsAsync(id))
            return NotFound("Recording not found");
        
        var user = await usersRepo.GetUserByEmailAsync(email!);
        if (user is null)
            return Unauthorized("User does not exist");
        
        if (!await recordingsRepo.IsOwnerAsync(id, user.Id) && !user.IsAdmin)
            return Unauthorized("You are not owner or admin to delete this recording");
        
        if (final && !user.IsAdmin)
            return Unauthorized("You cannot finally delete this recording if you are not admin");

        bool deleted = await recordingsRepo.DeleteAsync(id, final);
        
        return deleted ? Ok() : Conflict();
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadAsync([FromBody] RecordingUploadRequest request,
        [FromServices] JwtService jwtService,
        [FromServices] RecordingsRepository recordingsRepo,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        var user = await usersRepo.GetUserByEmailAsync(email!);
        if (user is null)
            return Unauthorized("User does not exist");
        
        int? recordingId = await recordingsRepo.UploadAsync(user.Id, request);
        
        if (recordingId is null)
            return StatusCode(409, "Failed to upload recording");
        
        Logger.Log($"Recording {recordingId} has been uploaded");

        return Ok(recordingId);
    }

    [HttpPost("upload-part")]
    [RequestSizeLimit(int.MaxValue)]
    public async Task<IActionResult> UploadPartAsync([FromBody] RecordingPartUploadRequest request,
        [FromServices] JwtService jwtService,
        [FromServices] RecordingsRepository recordingsRepo)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null) 
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out _))
            return Unauthorized();
        
        int? recordingPartId = await recordingsRepo.UploadPartAsync(request);
        
        if (recordingPartId is null)
            return StatusCode(500, "Failed to upload recording");

        Logger.Log($"Recording part {recordingPartId} has been uploaded");
        
        return Ok(recordingPartId);
    }

    [HttpPatch("{id:int}/edit")]
    public async Task<IActionResult> EditAsync([FromRoute] int id,
        [FromBody] UpdateRecordingRequest request,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo,
        [FromServices] RecordingsRepository recordingsRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string? email)) 
            return Unauthorized();
        
        var user = await usersRepo.GetUserByEmailAsync(email!);
        if (user is null)
            return Unauthorized("User does not exist");
        
        if (user.Email != email && !user.IsAdmin)
            return Unauthorized("User does not belong to this email or is not an admin");

        bool updated = await recordingsRepo.UpdateAsync(id, request);
        
        return updated ? Ok() : Conflict();
    }
}