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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Repository;
using Shared.BackgroundServices.AudioProcessing;
using Shared.Extensions;
using Shared.Models.Requests.Notifications;
using Shared.Models.Requests.Recordings;
using Shared.Tools;

namespace Utils;

[ApiController]
[Route("utils")]
public class UtilsController : ControllerBase
{
    private readonly ILogger<UtilsController> _logger;

    public UtilsController(ILogger<UtilsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Health check endpoint.
    /// </summary>
    /// <returns>200 if the service is up.</returns>
    [HttpHead("health")]
    public IActionResult Health() => Ok();

    /// <summary>
    /// Maintenance task that fixes recording parts sharing identical dates. Requires a valid JWT
    /// belonging to an administrator.
    /// </summary>
    /// <returns>200 on success, 400 if the JWT is missing, or 401 if it is invalid or the caller is not an admin.</returns>
    [HttpGet("fix-same-dates")]
    public async Task<IActionResult> FixSameDates([FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo,
        [FromServices] RecordingsRepository recordingsRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        if (!await usersRepo.IsAdminAsync(email))
            return Unauthorized("User is not admin");

        await recordingsRepo.FixSameDatesInPartsAsync();
        return Ok();
    }

    /// <summary>
    /// Maintenance task that normalizes the volume of existing recording audio files. Requires a valid
    /// JWT belonging to an administrator.
    /// </summary>
    /// <returns>200 on success, 400 if the JWT is missing, or 401 if it is invalid or the caller is not an admin.</returns>
    [HttpGet("normalize-existing-audios")]
    public async Task<IActionResult> NormalizeExistingAudios([FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo,
        [FromServices] RecordingsRepository recordingsRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        if (!await usersRepo.IsAdminAsync(email))
            return Unauthorized("User is not admin");

        await recordingsRepo.NormalizeAudiosAsync();
        return Ok();
    }

    /// <summary>
    /// Maintenance task that re-analyzes existing recording parts. Requires a valid JWT belonging to
    /// an administrator.
    /// </summary>
    /// <returns>200 on success, 400 if the JWT is missing, or 401 if it is invalid or the caller is not an admin.</returns>
    [HttpGet("analyze-parts")]
    public async Task<IActionResult> AnalyzeParts([FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo,
        [FromServices] RecordingsRepository recordingsRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        if (!await usersRepo.IsAdminAsync(email))
            return Unauthorized("User is not admin");

        await recordingsRepo.AnalyzePartsAsync();
        return Ok();
    }

    /// <summary>
    /// Sends a custom push notification to all of a user's registered devices. Requires a valid JWT
    /// belonging to an administrator.
    /// </summary>
    /// <param name="req">Identifies the target user and carries the localized notification title/body per language.</param>
    /// <returns>200 on success (even if individual device sends fail), 400 if the JWT is missing, 401 if it is invalid or the caller is not an admin, or 409 if the user's devices could not be loaded.</returns>
    [HttpPost("send-notification")]
    public async Task<IActionResult> SendNotification([FromBody] SendNotificationRequest req,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo,
        [FromServices] DevicesRepository devicesRepo,
        [FromServices] FirebaseNotificationService notificationService)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        if (!await usersRepo.IsAdminAsync(email))
            return Unauthorized("User is not admin");

        var devices = await devicesRepo.GetAllByUserIdAsync(req.UserId);
        if (devices is null)
            return Conflict();

        foreach (var device in devices)
        {
            try
            {
                await notificationService.SendInvisibleNotificationAsync(device.FcmToken,
                    new Dictionary<string, string?>
                    {
                        { "action", "custom" },
                        { "titleEn", req.TitleEn },
                        { "bodyEn", req.BodyEn },
                        { "titleDe", req.TitleDe },
                        { "bodyDe", req.BodyDe },
                        { "titleCs", req.TitleCs },
                        { "bodyCs", req.BodyCs },
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification");
            }
        }

        return Ok();
    }

    /// <summary>
    /// Enqueues all recordings prepared for classification to run through the dialect classification
    /// model. Requires a valid JWT belonging to an administrator.
    /// </summary>
    /// <returns>The recordings that were enqueued, 400 if the JWT is missing, 401 if it is invalid or the caller is not an admin, or 409 if the recordings could not be loaded.</returns>
    [HttpGet("classify")]
    public async Task<IActionResult> ClassifyRecordings([FromServices] AudioProcessingQueue queue,
        [FromServices] RecordingsRepository recordingsRepo,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        if (!await usersRepo.IsAdminAsync(email))
            return Unauthorized("User is not admin");

        var recordings = await recordingsRepo.GetPreparedForClassificationAsync();
        if (recordings is null)
            return Conflict();

        foreach (var recording in recordings)
        {
            if (recording.Parts is null)
                continue;

            foreach (var part in recording.Parts)
            {
                if (part.FilePath is null) continue;

                byte[] content = await System.IO.File.ReadAllBytesAsync(part.FilePath);
                await queue.EnqueueAsync(async sp =>
                {
                    var connector = sp.GetRequiredService<AiModelConnector>();
                    var logger = sp.GetRequiredService<ILogger<AudioProcessingService>>();
                    logger.LogInformation("Starting audio processing for recording part " + part.Id);
                    var result = await connector.Classify(content, part.FilePath);
                    if (result is null)
                        return;

                    var repo = sp.GetRequiredService<RecordingsRepository>();
                    await repo.ProcessPredictionAsync(part.Id, result);
                    logger.LogInformation($"Finished audio processing for recording part {part.Id}\nPrediction result: " + JsonSerializer.Serialize(result));
                });
            }
        }

        return Ok(recordings);
    }

    /// <summary>
    /// Maintenance task that re-runs the AI dialect classifier for detected dialects whose predicted
    /// dialect currently matches <paramref name="predictedDialectId"/>, replacing it with a freshly
    /// predicted value. Intended to repair bulk data mistakes made against predicted_dialect_id.
    /// If <paramref name="predictedDialectId"/> is omitted, every detected dialect is reclassified.
    /// Requires a valid JWT belonging to an administrator.
    /// </summary>
    /// <returns>The detected dialects that were enqueued for reclassification, 400 if the JWT is missing, 401 if it is invalid or the caller is not an admin, or 409 if the detected dialects could not be loaded.</returns>
    [HttpGet("reclassify-detected-dialects")]
    public async Task<IActionResult> ReclassifyDetectedDialects(
        [FromQuery] int? predictedDialectId,
        [FromServices] AudioProcessingQueue queue,
        [FromServices] RecordingsRepository recordingsRepo,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        if (!await usersRepo.IsAdminAsync(email))
            return Unauthorized("User is not admin");

        var detectedDialects = await recordingsRepo.GetDetectedDialectsByPredictedIdAsync(predictedDialectId);
        if (detectedDialects is null)
            return Conflict();

        foreach (var detectedDialect in detectedDialects)
        {
            var filteredPart = await recordingsRepo.GetFilteredPartAsync(detectedDialect.FilteredRecordingPartId);
            if (filteredPart is null)
            {
                _logger.LogWarning($"Skipping detected dialect {detectedDialect.Id}: filtered part {detectedDialect.FilteredRecordingPartId} not found");
                continue;
            }

            var parentPart = await recordingsRepo.FindParentPartAsync(filteredPart.RecordingId, filteredPart.StartDate, filteredPart.EndDate);
            if (parentPart?.FilePath is null)
            {
                _logger.LogWarning($"Skipping detected dialect {detectedDialect.Id}: no parent recording part found for filtered part {filteredPart.Id}");
                continue;
            }

            await queue.EnqueueAsync(async sp =>
            {
                var connector = sp.GetRequiredService<AiModelConnector>();
                var logger = sp.GetRequiredService<ILogger<AudioProcessingService>>();
                var repo = sp.GetRequiredService<RecordingsRepository>();

                string segmentPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"reclassify_{detectedDialect.Id}_{Guid.NewGuid():N}.wav");

                try
                {
                    await FFmpegService.ExtractSegmentAsync(parentPart.FilePath!,
                        filteredPart.StartDate - parentPart.StartDate,
                        filteredPart.EndDate - parentPart.StartDate,
                        segmentPath);

                    byte[] content = await System.IO.File.ReadAllBytesAsync(segmentPath);

                    logger.LogInformation($"Reclassifying detected dialect {detectedDialect.Id} (filtered part {filteredPart.Id})");
                    var result = await connector.Classify(content, segmentPath);

                    var segment = result?.Segments.FirstOrDefault(s => s.IsRepresentant) ?? result?.Segments.FirstOrDefault();
                    if (segment?.Label is null)
                    {
                        logger.LogWarning($"Reclassification produced no result for detected dialect {detectedDialect.Id}");
                        return;
                    }

                    int? newDialectId = await repo.GetDialectCodeIdAsync(segment.Label);
                    if (newDialectId is null)
                    {
                        logger.LogWarning($"Unknown dialect code '{segment.Label}' returned for detected dialect {detectedDialect.Id}");
                        return;
                    }

                    await repo.UpdateDetectedDialectAsync(new UpdateDetectedDialectRequest
                    {
                        Id = detectedDialect.Id,
                        PredictedDialectId = newDialectId
                    });

                    logger.LogInformation($"Reclassified detected dialect {detectedDialect.Id}: predicted_dialect_id -> {newDialectId} ({segment.Label})");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Failed to reclassify detected dialect {detectedDialect.Id}");
                }
                finally
                {
                    if (System.IO.File.Exists(segmentPath))
                        System.IO.File.Delete(segmentPath);
                }
            });
        }

        return Ok(detectedDialects);
    }
}
