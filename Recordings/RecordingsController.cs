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

/// <summary>
/// Provides endpoints for recording metadata, recording parts, audio files, and dialect lookup data.
/// </summary>
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
    /// Gets completed, non-deleted recordings, optionally filtered by user and expanded with parts or sound data.
    /// </summary>
    /// <param name="repo">Repository used to read recordings.</param>
    /// <param name="userId">Optional user identifier to filter recordings by owner.</param>
    /// <param name="parts">Whether to include recording parts.</param>
    /// <param name="sound">Whether to include part audio data when parts are requested.</param>
    /// <returns>An HTTP response containing recordings, no content when none exist, or an error status.</returns>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
    /// Gets recordings marked as deleted for an authenticated administrator.
    /// </summary>
    /// <param name="jwtService">Service used to validate the bearer JWT.</param>
    /// <param name="usersRepo">Repository used to resolve and authorize the current user.</param>
    /// <param name="recordingsRepo">Repository used to read deleted recordings.</param>
    /// <returns>An HTTP response containing deleted recordings, no content, or an authentication or error status.</returns>
    [HttpGet("deleted")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
    /// Gets one non-deleted recording by identifier, optionally expanded with parts or sound data.
    /// </summary>
    /// <param name="id">Recording identifier.</param>
    /// <param name="repo">Repository used to read the recording.</param>
    /// <param name="parts">Whether to include recording parts.</param>
    /// <param name="sound">Whether to include part audio data when parts are requested.</param>
    /// <returns>An HTTP response containing the recording, or no content when it does not exist or is deleted.</returns>
    [HttpGet("{id:int}")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
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
    /// Gets the WAV audio file for a recording part using the legacy route.
    /// </summary>
    /// <param name="recId">Recording identifier from the legacy route.</param>
    /// <param name="partId">Recording part identifier.</param>
    /// <param name="repo">Repository used to locate the recording part file.</param>
    /// <returns>An HTTP response streaming the WAV file, or not found when the file is unavailable.</returns>
    [Obsolete("use part/{partId:int} GET instead")]
    [HttpGet("part/{recId:int}/{partId:int}/sound")]
    [Produces("audio/wav")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    /// Gets the WAV audio file for a recording part.
    /// </summary>
    /// <param name="partId">Recording part identifier.</param>
    /// <param name="repo">Repository used to locate the recording part file.</param>
    /// <returns>An HTTP response streaming the WAV file, or not found when the file is unavailable.</returns>
    [HttpGet("part/{partId:int}/sound")]
    [Produces("audio/wav")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSound([FromRoute] int partId, [FromServices] RecordingsRepository repo)
    {
        var part = await repo.GetPartAsync(partId);
        if (part?.FilePath is null)
            return NotFound();

        return PhysicalFile(part.FilePath, "audio/wav", enableRangeProcessing: true);
    }

    /// <summary>
    /// Deletes or permanently deletes a recording when the authenticated user is the owner or an administrator.
    /// </summary>
    /// <param name="id">Recording identifier.</param>
    /// <param name="jwtService">Service used to validate the bearer JWT.</param>
    /// <param name="usersRepo">Repository used to resolve and authorize the current user.</param>
    /// <param name="recordingsRepo">Repository used to check ownership and delete the recording.</param>
    /// <param name="final">Whether to permanently delete instead of marking the recording as deleted.</param>
    /// <returns>An HTTP response indicating deletion success, authorization failure, missing recording, or conflict.</returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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
    /// Creates a recording for the authenticated user and schedules a later completeness check when possible.
    /// </summary>
    /// <param name="request">Recording metadata to upload.</param>
    /// <param name="jwtService">Service used to validate the bearer JWT.</param>
    /// <param name="recordingsRepo">Repository used to create the recording.</param>
    /// <param name="usersRepo">Repository used to resolve the current user.</param>
    /// <returns>An HTTP response containing the new recording identifier, or an authentication or conflict status.</returns>
    [HttpPost]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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
    /// Uploads a recording part with Base64-encoded audio for an authenticated request.
    /// </summary>
    /// <param name="request">Recording part metadata and Base64 audio data.</param>
    /// <param name="jwtService">Service used to validate the bearer JWT.</param>
    /// <param name="recordingsRepo">Repository used to create the part and store its audio.</param>
    /// <returns>An HTTP response containing the new part identifier, or an authentication or error status.</returns>
    [HttpPost("part")]
    [RequestSizeLimit(int.MaxValue)]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
    /// Uploads a recording part with multipart audio data and queues automatic audio classification.
    /// </summary>
    /// <param name="request">Recording part metadata from the submitted form.</param>
    /// <param name="file">Uploaded audio file for the recording part.</param>
    /// <param name="jwtService">Service used to validate the bearer JWT.</param>
    /// <param name="recordingsRepo">Repository used to create the part and store its audio.</param>
    /// <param name="modelConnector">AI model connector used by the queued classification task.</param>
    /// <param name="audioProcessingQueue">Queue used to run audio classification in the background.</param>
    /// <returns>An HTTP response containing the new part identifier, or an authentication or error status.</returns>
    [HttpPost("part-new")]
    [RequestSizeLimit(int.MaxValue)]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
    /// Gets identifiers of recordings owned by the authenticated user that have fewer parts than expected.
    /// </summary>
    /// <param name="recordingsRepo">Repository used to read incomplete recordings.</param>
    /// <param name="jwtService">Service used to validate the bearer JWT.</param>
    /// <param name="usersRepo">Repository used to resolve the current user.</param>
    /// <returns>An HTTP response containing incomplete recording identifiers, no content, or an authentication or error status.</returns>
    [HttpGet("incomplete")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
    /// Updates recording metadata when the authenticated user is authorized for the recording.
    /// </summary>
    /// <param name="id">Recording identifier.</param>
    /// <param name="request">Recording fields to update.</param>
    /// <param name="jwtService">Service used to validate the bearer JWT.</param>
    /// <param name="usersRepo">Repository used to resolve the current user and recording owner.</param>
    /// <param name="recordingsRepo">Repository used to read and update the recording.</param>
    /// <returns>An HTTP response indicating update success, authorization failure, missing recording, or conflict.</returns>
    [HttpPatch("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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
    /// Gets all configured dialects.
    /// </summary>
    /// <param name="recordingsRepo">Repository used to read dialect records.</param>
    /// <returns>An HTTP response containing dialect records.</returns>
    [HttpGet("dialects")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDialects([FromServices] RecordingsRepository recordingsRepo)
    {
        return Ok(await recordingsRepo.GetDialectsAsync());
    }
}
