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
using Microsoft.AspNetCore.Http;
using Repository;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using Recordings.Jobs;
using Shared.Extensions;
using Shared.Logging;
using Shared.Models.Requests.Recordings;
using Shared.Tools;
using System.Text.Json;

namespace Recordings;

[ApiController]
[Route("recordings")]
public class RecordingsController : ControllerBase
{
    private readonly ISchedulerFactory _schedulerFactory;

    public RecordingsController(ISchedulerFactory schedulerFactory)
    {
        _schedulerFactory = schedulerFactory;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecordingsAsync([FromServices] RecordingsRepository repo,
        [FromQuery] int? userId = null,
        [FromQuery] bool parts = false,
        [FromQuery] bool sound = false)
    {
        var recordings = (await repo.GetAsync(userId, parts, sound))?.Where(r => !r.Deleted).ToArray();
        
        if (recordings is null)
            return StatusCode(500, "Failed to get recordings");

        if (recordings.Length is 0)
            return NoContent();
        
        return Ok(recordings);
    }

    [HttpGet("deleted")]
    public async Task<IActionResult> GetDeletedAsync([FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo,
        [FromServices] RecordingsRepository recordingsRepo,
        [FromQuery] int? userId = null,
        [FromQuery] bool parts = false,
        [FromQuery] bool sound = false)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        var user = await usersRepo.GetUserByEmailAsync(email!);
        if (user is null)
            return BadRequest("Invalid email");
        
        if (!user.IsAdmin)
            return Unauthorized("User is not an admin");

        var recordings = await recordingsRepo.GetAsync(userId, parts, sound);
        
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
        var recording = await repo.GetByIdAsync(id, parts, sound);
        
        if (recording is null || recording.Deleted)
            return NoContent();
        
        return Ok(recording);
    }

    [HttpGet("part/{recId:int}/{partId:int}/sound")]
    public async Task<IActionResult> GetSound([FromRoute] int recId,
        [FromRoute] int partId,
        [FromServices] RecordingsRepository repo)
    {
        var recordingPart = await repo.GetPartSoundAsync(partId);

        return File(recordingPart, "audio/wav");
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
        
        Logger.Log(deleted ? $"Deleted recording {id}" : $"Failed to delete recording {id}");
        
        return deleted ? Ok() : Conflict();
    }

    [HttpPost]
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
        
        Logger.Log(recordingId is not null
            ? $"Recording {recordingId} has been uploaded"
            : $"Failed to upload recording {recordingId}");
        
        if (recordingId is null)
            return StatusCode(409, "Failed to upload recording");

        await ScheduleRecordingCheckAsync(recordingId.Value, fcmToken: request.DeviceId!);

        return Ok(recordingId);
    }

    private async Task ScheduleRecordingCheckAsync(int recordingId, string fcmToken)
    {
        if (string.IsNullOrEmpty(fcmToken))
            return;

        var scheduler = await _schedulerFactory.GetScheduler();
        
        var job = JobBuilder.Create<CheckRecordingJob>()
            .WithIdentity($"check_recording_{recordingId}", "group1")
            .UsingJobData("recordingId", recordingId.ToString())
            .UsingJobData("fcmToken", fcmToken)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"trigger_check_recording_{recordingId}", "group1")
            .StartAt(DateBuilder.FutureDate(1, IntervalUnit.Hour))
            .Build();

        Logger.Log("Scheduling", LogLevel.Debug);
        await scheduler.ScheduleJob(job, trigger);
        Logger.Log("Scheduled", LogLevel.Debug);
    }

    [HttpPost("part")]
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

        Logger.Log(recordingPartId is not null
            ? $"Recording part {recordingPartId} has been uploaded"
            : $"Failed to upload recording part {recordingPartId}");
        
        return recordingPartId is not null 
            ? Ok(recordingPartId) 
            : StatusCode(500, "Failed to upload recording");
    }

    [HttpPost("part-new")]
    [RequestSizeLimit(int.MaxValue)]
    public async Task<IActionResult> UploadPartAsync([FromForm] RecordingPartUploadRequest request,
        IFormFile file,
        [FromServices] JwtService jwtService,
        [FromServices] RecordingsRepository recordingsRepo,
        [FromServices] AiModelConnector modelConnector)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null) 
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out _))
            return Unauthorized();
        
        int? recordingPartId = await recordingsRepo.UploadPartAsync(request, file);

        Logger.Log(recordingPartId is not null
            ? $"Recording part {recordingPartId} has been uploaded"
            : $"Failed to upload recording part {recordingPartId}");
        
        if (recordingPartId is null)
            return StatusCode(500, "Failed to upload recording");

        SendRecordingToClassificationAsync(recordingPartId.Value, recordingsRepo, modelConnector);
        return Ok(recordingPartId);
    }

    private async void SendRecordingToClassificationAsync(int recordingPartId, RecordingsRepository repo, AiModelConnector modelConnector)
    {
        try
        {
            var part = await repo.GetPartSoundAsync(recordingPartId);
            if (part is null)
                return;
        
            var result = await modelConnector.ClassifyAsync(part);
            Logger.Log("Classification result: " + JsonSerializer.Serialize(result));
        }
        catch (Exception e)
        {
            // Ignore
        }
    }

    [HttpGet("incomplete")]
    public async Task<IActionResult> GetIncompleteRecordingsAsync([FromServices] RecordingsRepository recordingsRepo, 
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        var user = await usersRepo.GetUserByEmailAsync(email);
        if (user is null)
            return Unauthorized("User does not exist");
        
        var recordings = await recordingsRepo.GetIncompleteRecordingsAsync(user.Id);
        
        if (recordings is null)
            return StatusCode(500, "Failed to get incomplete recordings");

        if (recordings.Length is 0)
            return NoContent();
        
        return Ok(recordings);
    }

    [HttpPatch("{id:int}")]
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
        
        var jwtUser = await usersRepo.GetUserByEmailAsync(email!);
        if (jwtUser is null)
            return Unauthorized("User does not exist");

        var recording = await recordingsRepo.GetByIdAsync(id, parts: false, sound: false);
        if (recording is null)
            return NotFound("Recording not found");

        var recordingOwner = (await usersRepo.GetUserByIdAsync(recording.UserId))!;
        if (jwtUser.Email != recordingOwner.Email || !jwtUser.IsAdmin)
            return Unauthorized("User does not belong to this email or is not an admin");

        bool updated = await recordingsRepo.UpdateAsync(id, request);
        
        Logger.Log(updated ? $"Recording {id} updated successfully" : $"Failed to update recording {id}");
        
        return updated ? Ok() : Conflict();
    }

    [HttpGet("dialects")]
    public async Task<IActionResult> GetDialects([FromServices] RecordingsRepository recordingsRepo)
    {
        return Ok(await recordingsRepo.GetDialectsAsync());
    }
}