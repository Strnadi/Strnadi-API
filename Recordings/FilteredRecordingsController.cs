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
using Microsoft.AspNetCore.Mvc;
using Repository;
using Shared.Extensions;
using Shared.Logging;
using Shared.Models.Database.Recordings;
using Shared.Models.Requests.Recordings;
using LogLevel = Shared.Logging.LogLevel;

namespace Recordings;

/// <summary>
/// Provides endpoints for filtered recording parts and detected dialect records.
/// </summary>
[ApiController]
[Route("/recordings/filtered")]
public class FilteredRecordingsController : ControllerBase
{
    /// <summary>
    /// Gets filtered recording parts, optionally limited by recording and verified states.
    /// </summary>
    /// <param name="recordingsRepo">Repository used to read filtered recording parts.</param>
    /// <param name="recordingId">Optional recording identifier to filter by.</param>
    /// <param name="verified">Whether to include only filtered parts in verified states.</param>
    /// <returns>An HTTP response containing filtered parts, no content, or a conflict status.</returns>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetFilteredPartsAsync([FromServices] RecordingsRepository recordingsRepo,
        [FromQuery] int? recordingId = null,
        [FromQuery] bool verified = false)
    {
        var filtered = await recordingsRepo.GetFilteredPartsAsync(recordingId, verified);
        
        if (filtered is null) 
            return StatusCode(409, "Failed to get filtered parts");

        if (filtered.Length is 0)
            return NoContent();

        return Ok(filtered);
    }

    /// <summary>
    /// Gets one filtered recording part by identifier.
    /// </summary>
    /// <param name="fpId">Filtered recording part identifier.</param>
    /// <param name="recordingsRepo">Repository used to read the filtered recording part.</param>
    /// <returns>An HTTP response containing the filtered part, or conflict when it is unavailable.</returns>
    [HttpGet("{fpId:int}")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetFilteredPartAsync([FromRoute] int fpId, [FromServices] RecordingsRepository recordingsRepo)
    {
        var fp = await recordingsRepo.GetFilteredPartAsync(fpId);
        return fp is not null ? Ok(fp) : Conflict();
    }

    /// <summary>
    /// Uploads a filtered recording part for an authenticated request.
    /// </summary>
    /// <param name="model">Filtered recording part data to upload.</param>
    /// <param name="jwtService">Service used to validate the bearer JWT.</param>
    /// <param name="recordingsRepo">Repository used to create the filtered part.</param>
    /// <returns>An HTTP response indicating upload success, authentication failure, or conflict.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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
    
    /// <summary>
    /// Creates a manually confirmed filtered part and detected dialect for an administrator.
    /// </summary>
    /// <param name="req">Confirmed dialect and filtered part data.</param>
    /// <param name="jwtService">Service used to validate the bearer JWT.</param>
    /// <param name="usersRepo">Repository used to authorize the current user as an administrator.</param>
    /// <param name="recordingsRepo">Repository used to create filtered part and detected dialect records.</param>
    /// <returns>An HTTP response indicating creation success or validation, authorization, conflict, or server error.</returns>
    [HttpPost("post-confirmed-dialect")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PostConfirmedDialectAsync([FromBody] PostConfirmedDialectRequest req,
        [FromServices] JwtService jwtService,
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

        if (!await recordingsRepo.ExistsAsync(req.RecordingId))
            return Conflict("Recording does not exist");

        var part = await recordingsRepo.CreateFilteredPartAsync(
            req.RecordingId,
            req.StartDate,
            req.EndDate,
            FilteredRecordingPartState.ConfirmedManually,
            req.Representant
        );

        var dialectId = await recordingsRepo.GetDialectCodeIdAsync(req.DialectCode);
        if (dialectId is null)
            return BadRequest("Invalid dialect code");

        bool createdDetected = await recordingsRepo.InsertDetectedDialectAsync(part!.Id, userGuessDialectId: null, confirmedDialectId: dialectId, predictedDialectId: null);
        if (!createdDetected)
        {
            Logger.Log("FilteredRecordingsController::InsertDetectedDialectAsync returned false", LogLevel.Error);
            return StatusCode(500);
        }
        
        return Ok();
    }

    /// <summary>
    /// Updates confirmed dialect data and selected filtered part fields for an administrator.
    /// </summary>
    /// <param name="req">Confirmed dialect update request.</param>
    /// <param name="jwtService">Service used to validate the bearer JWT.</param>
    /// <param name="usersRepo">Repository used to authorize the current user as an administrator.</param>
    /// <param name="recordingsRepo">Repository used to update filtered part and detected dialect records.</param>
    /// <returns>An HTTP response indicating update success or validation, authorization, conflict, or server error.</returns>
    [HttpPatch("update-confirmed-dialect")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateConfirmedDialectAsync([FromBody] UpdateConfirmedDialectRequest req,
        [FromServices] JwtService jwtService,
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
        
        if (!await recordingsRepo.ExistsFilteredPartAsync(req.FilteredPartId)) 
            return Conflict("Filtered part does not exist");
        
        if (req.StartDate == null && req.EndDate == null && req.Representant == null && req.ConfirmedDialectCode == null)
            return Ok();

        if (req.ConfirmedDialectCode is not null)
        {
            var dialectId = await recordingsRepo.GetDialectCodeIdAsync(req.ConfirmedDialectCode);
            if (dialectId is null)
                return BadRequest("Invalid dialect code");
        }

        if (req.Representant != null || req.StartDate != null || req.EndDate != null)
        {
            bool updated = await recordingsRepo.UpdateFilteredPartAsync(
                req.FilteredPartId, 
                req.StartDate, 
                req.EndDate, 
                req.Representant, 
                state: null, 
                recordingId: null, 
                parentId: null
            );
            Logger.Log("Updated Filtered part with id " + req.FilteredPartId);
            if (!updated)
            {
                Logger.Log("FilteredRecordingsController::UpdateConfirmedDialectAsync: UpdateFilteredPartsAsync returned false", LogLevel.Warning);
                return StatusCode(500);
            }
        }
        
        if (req.ConfirmedDialectCode != null)
        {
            bool updated = await recordingsRepo.UpsertDetectedDialectAsync(
                req.FilteredPartId, 
                confirmedDialectCode: req.ConfirmedDialectCode);
            
            if (!updated)
            {
                Logger.Log("FilteredRecordingsController::UpdateConfirmedDialectAsync: SetConfirmedDialect returned false", LogLevel.Warning);
                return StatusCode(500);
            }
        }

        return Ok();
    }
    
    /// <summary>
    /// Updates selected fields of a filtered recording part for an administrator.
    /// </summary>
    /// <param name="fpId">Filtered recording part identifier.</param>
    /// <param name="req">Filtered part fields to update.</param>
    /// <param name="jwtService">Service used to validate the bearer JWT.</param>
    /// <param name="usersRepo">Repository used to authorize the current user as an administrator.</param>
    /// <param name="recordingsRepo">Repository used to update the filtered part.</param>
    /// <returns>An HTTP response indicating update success or authentication, authorization, conflict, or server error.</returns>
    [HttpPatch("{fpId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PatchFilteredPartAsync([FromRoute] int fpId,
        [FromBody] FilteredRecordingPartUpdateRequest req,
        [FromServices] JwtService jwtService,
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
        
        if (!await recordingsRepo.ExistsFilteredPartAsync(fpId)) 
            return Conflict("Filtered part does not exist");
        
        if (req.StartDate == null && req.EndDate == null && 
            req.Representant == null && req.RecordingId == null &&
            req.ParentId == null && req.State == null)
            return Ok();

        bool updated = await recordingsRepo.UpdateFilteredPartAsync(
            fpId, 
            req.StartDate, 
            req.EndDate, 
            req.Representant, 
            req.State, 
            req.RecordingId, 
            req.ParentId
        );
        
        return updated ? Ok() : StatusCode(500);
    }

    /// <summary>
    /// Deletes a filtered recording part using the legacy confirmed dialect route.
    /// </summary>
    /// <param name="filteredPartId">Filtered recording part identifier.</param>
    /// <param name="jwtService">Service used to validate the bearer JWT.</param>
    /// <param name="usersRepo">Repository used to authorize the current user as an administrator.</param>
    /// <param name="recordingsRepo">Repository used to delete the filtered part.</param>
    /// <returns>An HTTP response indicating deletion success or authentication, authorization, or conflict status.</returns>
    [Obsolete("Use /recordings/filtered/{fpId} DELETE instead")]
    [HttpDelete("delete-confirmed-dialect/{filteredPartId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteConfirmedDialectAsync([FromRoute] int filteredPartId,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo,
        [FromServices] RecordingsRepository recordingsRepo)
    {
        return await DeleteFilteredPartAsync(filteredPartId, recordingsRepo, usersRepo, jwtService);
    }

    /// <summary>
    /// Deletes a filtered recording part for an administrator.
    /// </summary>
    /// <param name="fpId">Filtered recording part identifier.</param>
    /// <param name="recordingsRepo">Repository used to delete the filtered part.</param>
    /// <param name="usersRepo">Repository used to authorize the current user as an administrator.</param>
    /// <param name="jwtService">Service used to validate the bearer JWT.</param>
    /// <returns>An HTTP response indicating deletion success or authentication, authorization, or conflict status.</returns>
    [HttpDelete("{fpId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteFilteredPartAsync([FromRoute] int fpId, 
        [FromServices] RecordingsRepository recordingsRepo, 
        [FromServices] UsersRepository usersRepo, 
        [FromServices] JwtService jwtService)
    {
        string? jwt = this.GetJwt();
        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();
        
        if (!await recordingsRepo.ExistsFilteredPartAsync(fpId))
            return Conflict("Filtered part does not exist");
        
        if (!await usersRepo.IsAdminAsync(email))
            return Unauthorized("User is not admin");
        
        bool deleted =  await recordingsRepo.DeleteFilteredPartAsync(fpId);

        return deleted ? Ok() : Conflict();
    }

    /// <summary>
    /// Gets all detected dialect records.
    /// </summary>
    /// <param name="repo">Repository used to read detected dialects.</param>
    /// <returns>An HTTP response containing detected dialect records, or conflict when unavailable.</returns>
    [HttpGet("detected/")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetDetectedDialectsAsync([FromServices] RecordingsRepository repo)
    {
        var detected = await repo.GetDetectedDialectsAsync();
        return detected is not null ? Ok(detected) : Conflict();
    }

    /// <summary>
    /// Gets detected dialect records by detected dialect identifier.
    /// </summary>
    /// <param name="ddId">Detected dialect identifier.</param>
    /// <param name="repo">Repository used to read detected dialects.</param>
    /// <returns>An HTTP response containing matching detected dialect records, or conflict when unavailable.</returns>
    [HttpGet("detected/{ddId:int}")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetDetectedDialectAsync([FromRoute] int ddId, [FromServices] RecordingsRepository repo)
    {
        var detected = await repo.GetDetectedDialectsAsync(ddId);
        return detected is not null ? Ok(detected) : Conflict();
    }

    /// <summary>
    /// Creates a detected dialect record for an administrator.
    /// </summary>
    /// <param name="req">Detected dialect data to create.</param>
    /// <param name="usersRepo">Repository used to authorize the current user as an administrator.</param>
    /// <param name="jwtService">Service used to validate the bearer JWT.</param>
    /// <param name="repo">Repository used to create the detected dialect.</param>
    /// <returns>An HTTP response indicating creation success or authentication, authorization, or conflict status.</returns>
    [HttpPost("detected/")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PostDetectedDialectAsync([FromBody] DetectedDialectUploadRequest req,
        [FromServices] UsersRepository usersRepo,
        [FromServices] JwtService jwtService,
        [FromServices] RecordingsRepository repo)
    {
        string? jwt = this.GetJwt();
        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();
        
        if (!await usersRepo.IsAdminAsync(email))
            return Unauthorized("User is not admin");
        
        bool created = await repo.InsertDetectedDialectAsync(req.FilteredPartId, req.UserGuessDialectId, req.ConfirmedDialectId, req.PredictedDialectId);

        return created ? Created() : Conflict();
    }

    /// <summary>
    /// Updates selected fields of a detected dialect record for an administrator.
    /// </summary>
    /// <param name="req">Detected dialect fields to update.</param>
    /// <param name="usersRepo">Repository used to authorize the current user as an administrator.</param>
    /// <param name="jwtService">Service used to validate the bearer JWT.</param>
    /// <param name="repo">Repository used to update the detected dialect.</param>
    /// <returns>An HTTP response indicating update success or authentication, authorization, or conflict status.</returns>
    [HttpPatch("detected/")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PatchDetectedDialectsAsync([FromBody] UpdateDetectedDialectRequest req,
        [FromServices] UsersRepository usersRepo,
        [FromServices] JwtService jwtService,
        [FromServices] RecordingsRepository repo)
    {
        string? jwt = this.GetJwt();
        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();
        
        if (!await usersRepo.IsAdminAsync(email))
            return Unauthorized("User is not admin");

        bool updated = await repo.UpdateDetectedDialectAsync(req);
        
        return updated ? Ok() : Conflict();
    }

    /// <summary>
    /// Deletes a detected dialect record for an administrator.
    /// </summary>
    /// <param name="ddId">Detected dialect identifier.</param>
    /// <param name="usersRepo">Repository used to authorize the current user as an administrator.</param>
    /// <param name="jwtService">Service used to validate the bearer JWT.</param>
    /// <param name="repo">Repository used to delete the detected dialect.</param>
    /// <returns>An HTTP response indicating deletion success or authentication, authorization, or conflict status.</returns>
    [HttpDelete("detected/{ddId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteDetectedDialectsAsync([FromRoute] int ddId,
        [FromServices] UsersRepository usersRepo,
        [FromServices] JwtService jwtService,
        [FromServices] RecordingsRepository repo)
    {   
        string? jwt = this.GetJwt();
        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();
        
        if (!await usersRepo.IsAdminAsync(email))
            return Unauthorized("User is not admin");

        bool deleted = await repo.DeleteDetectedDialectAsync(ddId);
        
        return deleted ? Ok() : Conflict();
    }
}
