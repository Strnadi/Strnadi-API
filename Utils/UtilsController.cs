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
    
    [HttpHead("health")]
    public IActionResult Health() => Ok();

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
}