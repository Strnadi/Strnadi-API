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
using Shared.Models.Requests.Photos;

namespace Photos;

/// <summary>
/// Handles photo upload endpoints.
/// </summary>
[ApiController]
[Route("photos")]
public class PhotosController : ControllerBase
{
    /// <summary>
    /// Uploads and stores a photo for a recording.
    /// </summary>
    /// <param name="request">Recording photo data to upload.</param>
    /// <param name="repo">Repository used to save the photo metadata and file.</param>
    /// <param name="jwtService">JWT validation service.</param>
    /// <returns>An HTTP result indicating whether the recording photo was saved.</returns>
    [HttpPost("upload/recording-photo")]
    [RequestSizeLimit(130023424)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UploadRecPhoto([FromBody] UploadRecordingPhotoRequest request,
        [FromServices] PhotosRepository repo,
        [FromServices] JwtService jwtService)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        bool success = await repo.UploadRecPhotoAsync(request);
        if (success) Logger.Log($"Photo for recording {request.RecordingId} has been uploaded");
        
        return success ? Ok() : Conflict("Failed to save recording photo");
    }
}
