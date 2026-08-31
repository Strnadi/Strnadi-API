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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.BackgroundServices.AudioProcessing;
using LogLevel = Shared.Logging.LogLevel;

namespace Recordings;

[ApiController]
[Route("recordings")]
public class RecordingsController : ControllerBase
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<RecordingsController> _logger;

    public RecordingsController(ISchedulerFactory schedulerFactory, ILogger<RecordingsController> logger)
    {
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Lists non-deleted recordings, optionally filtered by owner.
    /// </summary>
    /// <param name="userId">Identifier of the user to filter recordings by. Omit to list all recordings.</param>
    /// <param name="parts">Whether to include each recording's parts.</param>
    /// <param name="sound">Whether to include the parts' audio data. Ignored unless <paramref name="parts"/> is <c>true</c>.</param>
    /// <returns>The array of recordings, 204 if none exist, or 500 on failure.</returns>
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

    /// <summary>
    /// Lists soft-deleted recordings. Requires a valid JWT belonging to an administrator.
    /// </summary>
    /// <returns>The array of deleted recordings, 204 if none exist, 400 if the JWT is missing, 401 if it is invalid or the caller is not an admin, or 500 on failure.</returns>
    [HttpGet("deleted")]
    public async Task<IActionResult> GetDeletedAsync([FromServices] JwtService jwtService,
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
            return BadRequest("Invalid email");

        if (!user.IsAdmin)
            return Unauthorized("User is not an admin");

        var recordings = await recordingsRepo.GetDeletedAsync();

        if (recordings is null)
            return StatusCode(500, "Failed to get recordings");

        if (recordings.Count() is 0)
            return NoContent();

        return Ok(recordings);
    }

    /// <summary>
    /// Gets a single recording by its identifier.
    /// </summary>
    /// <param name="id">Identifier of the recording to retrieve.</param>
    /// <param name="parts">Whether to include the recording's parts.</param>
    /// <param name="sound">Whether to include the parts' audio data. Ignored unless <paramref name="parts"/> is <c>true</c>.</param>
    /// <returns>The recording, or 204 if it does not exist or has been deleted.</returns>
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

    /// <summary>
    /// Downloads the audio for a recording part, addressed by both recording and part identifier.
    /// </summary>
    /// <param name="recId">Identifier of the recording the part belongs to.</param>
    /// <param name="partId">Identifier of the part to download audio for.</param>
    /// <returns>The audio as a WAV file, or 404 if it does not exist.</returns>
    [Obsolete("use part/{partId:int} GET instead")]
    [HttpGet("part/{recId:int}/{partId:int}/sound")]
    public async Task<IActionResult> GetSound([FromRoute] int recId,
        [FromRoute] int partId,
        [FromServices] RecordingsRepository repo)
    {
        var part = await repo.GetPartAsync(partId);
        if (part?.FilePath is null)
            return NotFound();
        return PhysicalFile(part.FilePath, "audio/wav", enableRangeProcessing: true);
    }

    /// <summary>
    /// Downloads the audio for a recording part.
    /// </summary>
    /// <param name="partId">Identifier of the part to download audio for.</param>
    /// <returns>The audio as a WAV file, or 404 if it does not exist.</returns>
    [HttpGet("part/{partId:int}/sound")]
    public async Task<IActionResult> GetSound([FromRoute] int partId, [FromServices] RecordingsRepository repo)
    {
        var part = await repo.GetPartAsync(partId);
        if (part?.FilePath is null)
            return NotFound();

        return PhysicalFile(part.FilePath, "audio/wav", enableRangeProcessing: true);
    }

    /// <summary>
    /// Deletes a recording. Requires a valid JWT belonging to the recording's owner or an administrator.
    /// </summary>
    /// <param name="id">Identifier of the recording to delete.</param>
    /// <param name="final">Whether to permanently delete the recording instead of soft-deleting it. Only administrators may pass <c>true</c>.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if it is invalid or the caller lacks permission, 404 if the recording does not exist, or 409 on failure.</returns>
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

    /// <summary>
    /// Uploads a new recording and schedules a follow-up check job. Requires a valid JWT.
    /// </summary>
    /// <param name="request">The recording metadata to store.</param>
    /// <returns>The identifier of the created recording, 400 if the JWT is missing, 401 if it is invalid or the user does not exist, or 409 on failure.</returns>
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

    /// <summary>
    /// Uploads a recording part as a JSON body. Requires a valid JWT.
    /// </summary>
    /// <param name="request">The recording part's metadata and audio data.</param>
    /// <returns>The identifier of the created part, 400 if the JWT is missing, 401 if it is invalid, or 500 on failure.</returns>
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

    /// <summary>
    /// Uploads a recording part along with its audio file as multipart form data, and enqueues it for
    /// automatic dialect classification. Requires a valid JWT.
    /// </summary>
    /// <param name="request">The recording part's metadata.</param>
    /// <param name="file">The part's audio file.</param>
    /// <returns>The identifier of the created part, 400 if the JWT is missing, 401 if it is invalid, or 500 on failure.</returns>
    [HttpPost("part-new")]
    [RequestSizeLimit(int.MaxValue)]
    public async Task<IActionResult> UploadPartAsync([FromForm] RecordingPartUploadRequest request,
        IFormFile file,
        [FromServices] JwtService jwtService,
        [FromServices] RecordingsRepository recordingsRepo,
        [FromServices] AiModelConnector modelConnector,
        [FromServices] AudioProcessingQueue audioProcessingQueue)
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

        await audioProcessingQueue.EnqueueAsync(async sp =>
            await ClassifyAudioAsync(recordingPartId.Value,
                sp.GetRequiredService<RecordingsRepository>(),
                sp.GetRequiredService<AiModelConnector>()
            ));

        return Ok(recordingPartId);
    }

    private async Task ClassifyAudioAsync(int recordingPartId, RecordingsRepository repo, AiModelConnector modelConnector)
    {
        try
        {
            var part = await repo.GetPartAsync(recordingPartId);
            if (part is null)
                return;


            var audio = await repo.GetPartSoundAsync(recordingPartId);
            if (audio is null)
                return;

            var result = await modelConnector.Classify(audio, part.FilePath);
            if (result is null)
                return;

            Logger.Log("Classification result: " + JsonSerializer.Serialize(result));
            await repo.ProcessPredictionAsync(recordingPartId, result);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to classify audio");
            // Ignore
        }
    }

    /// <summary>
    /// Lists recordings that the caller has started but not yet completed uploading. Requires a valid JWT.
    /// </summary>
    /// <returns>The array of incomplete recordings, 204 if none exist, 400 if the JWT is missing, 401 if it is invalid or the user does not exist, or 500 on failure.</returns>
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

    /// <summary>
    /// Updates a recording. Requires a valid JWT belonging to the recording's owner or an administrator.
    /// </summary>
    /// <param name="id">Identifier of the recording to update.</param>
    /// <param name="request">The fields to update.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if it is invalid or the caller lacks permission, 404 if the recording does not exist, or 409 on failure.</returns>
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

    /// <summary>
    /// Lists the known bird dialects.
    /// </summary>
    /// <returns>The array of dialects.</returns>
    [HttpGet("dialects")]
    public async Task<IActionResult> GetDialects([FromServices] RecordingsRepository recordingsRepo)
    {
        return Ok(await recordingsRepo.GetDialectsAsync());
    }
}
