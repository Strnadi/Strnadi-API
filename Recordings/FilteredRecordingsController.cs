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
using Shared.Models.Requests.Recordings;

namespace Recordings;

[ApiController]
[Route("/recordings/filtered")]
public class FilteredRecordingsController : ControllerBase
{
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
}