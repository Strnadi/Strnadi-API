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
using Repository;
using Shared.Extensions;
using Shared.Logging;
using Shared.Models.Database.Recordings;
using Shared.Models.Requests.Recordings;
using LogLevel = Shared.Logging.LogLevel;

namespace Recordings;

[ApiController]
[Route("/recordings/filtered")]
public class FilteredRecordingsController : ControllerBase
{
    /// <summary>
    /// Lists filtered recording parts, optionally scoped to a recording and/or verification state.
    /// </summary>
    /// <param name="recordingId">Identifier of the recording to filter parts by. Omit to list parts for all recordings.</param>
    /// <param name="verified">Whether to only return parts that have been verified.</param>
    /// <returns>The array of filtered parts, 204 if none exist, or 409 on failure.</returns>
    [HttpGet]
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
    /// Gets a single filtered recording part by its identifier.
    /// </summary>
    /// <param name="fpId">Identifier of the filtered part to retrieve.</param>
    /// <returns>The filtered part, or 409 if it does not exist.</returns>
    [HttpGet("{fpId:int}")]
    public async Task<IActionResult> GetFilteredPartAsync([FromRoute] int fpId, [FromServices] RecordingsRepository recordingsRepo)
    {
        var fp = await recordingsRepo.GetFilteredPartAsync(fpId);
        return fp is not null ? Ok(fp) : Conflict();
    }

    /// <summary>
    /// Uploads a filtered recording part. Requires a valid JWT.
    /// </summary>
    /// <param name="model">The filtered part to create.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if it is invalid, or 409 on failure.</returns>
    [HttpPost]
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
    /// Creates a manually confirmed dialect detection for a newly created filtered part. Requires a
    /// valid JWT belonging to an administrator.
    /// </summary>
    /// <param name="req">The recording, time range, representative sample, and confirmed dialect code.</param>
    /// <returns>200 on success, 400 if the JWT/dialect code is invalid, 401 if the caller is not an admin, 409 if the recording does not exist, or 500 on failure.</returns>
    [HttpPost("post-confirmed-dialect")]
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
    /// Updates the confirmed dialect and/or time range of a filtered part identified by its confirmed
    /// dialect entry. Requires a valid JWT belonging to an administrator.
    /// </summary>
    /// <param name="req">Identifies the filtered part and the fields to update.</param>
    /// <returns>200 on success, 400 if the JWT/dialect code is invalid, 401 if the caller is not an admin, 409 if the filtered part does not exist, or 500 on failure.</returns>
    [HttpPatch("update-confirmed-dialect")]
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
    /// Updates a filtered recording part. Requires a valid JWT belonging to an administrator.
    /// </summary>
    /// <param name="fpId">Identifier of the filtered part to update.</param>
    /// <param name="req">The fields to update.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if the caller is not an admin, 409 if the filtered part does not exist, or 500 on failure.</returns>
    [HttpPatch("{fpId:int}")]
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
    /// Deletes a filtered part's confirmed dialect entry. Requires a valid JWT belonging to an administrator.
    /// </summary>
    /// <param name="filteredPartId">Identifier of the filtered part to delete.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if the caller is not an admin, or 409 if it does not exist.</returns>
    [Obsolete("Use /recordings/filtered/{fpId} DELETE instead")]
    [HttpDelete("delete-confirmed-dialect/{filteredPartId:int}")]
    public async Task<IActionResult> DeleteConfirmedDialectAsync([FromRoute] int filteredPartId,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo,
        [FromServices] RecordingsRepository recordingsRepo)
    {
        return await DeleteFilteredPartAsync(filteredPartId, recordingsRepo, usersRepo, jwtService);
    }

    /// <summary>
    /// Deletes a filtered recording part. Requires a valid JWT belonging to an administrator.
    /// </summary>
    /// <param name="fpId">Identifier of the filtered part to delete.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if the caller is not an admin, or 409 if it does not exist.</returns>
    [HttpDelete("{fpId:int}")]
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
    /// Lists all detected dialect entries.
    /// </summary>
    /// <returns>The array of detected dialects, or 409 on failure.</returns>
    [HttpGet("detected/")]
    public async Task<IActionResult> GetDetectedDialectsAsync([FromServices] RecordingsRepository repo)
    {
        var detected = await repo.GetDetectedDialectsAsync();
        return detected is not null ? Ok(detected) : Conflict();
    }

    /// <summary>
    /// Gets a single detected dialect entry by its identifier.
    /// </summary>
    /// <param name="ddId">Identifier of the detected dialect entry to retrieve.</param>
    /// <returns>The detected dialect entry, or 409 if it does not exist.</returns>
    [HttpGet("detected/{ddId:int}")]
    public async Task<IActionResult> GetDetectedDialectAsync([FromRoute] int ddId, [FromServices] RecordingsRepository repo)
    {
        var detected = await repo.GetDetectedDialectsAsync(ddId);
        return detected is not null ? Ok(detected) : Conflict();
    }

    /// <summary>
    /// Creates a detected dialect entry for a filtered part. Requires a valid JWT belonging to an administrator.
    /// </summary>
    /// <param name="req">The filtered part and the user-guessed, confirmed, and/or predicted dialects.</param>
    /// <returns>201 on success, 400 if the JWT is missing, 401 if the caller is not an admin, or 409 on failure.</returns>
    [HttpPost("detected/")]
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
    /// Updates a detected dialect entry. Requires a valid JWT belonging to an administrator.
    /// </summary>
    /// <param name="req">Identifies the detected dialect entry and the fields to update.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if the caller is not an admin, or 409 on failure.</returns>
    [HttpPatch("detected/")]
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
    /// Deletes a detected dialect entry. Requires a valid JWT belonging to an administrator.
    /// </summary>
    /// <param name="ddId">Identifier of the detected dialect entry to delete.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if the caller is not an admin, or 409 on failure.</returns>
    [HttpDelete("detected/{ddId:int}")]
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
