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
using Shared.Models.Requests.Photos;

namespace Photos;

[ApiController]
[Route("photos")]
public class PhotosController : ControllerBase
{
    [HttpPost("upload/recording-photo")]
    [RequestSizeLimit(130023424)]
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