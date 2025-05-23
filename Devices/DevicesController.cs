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
using Microsoft.Extensions.Logging;
using Repository;
using Shared.Extensions;
using Shared.Logging;
using Shared.Models.Requests.Devices;

namespace Devices;

[ApiController]
[Route("/devices")]
public class DevicesController : ControllerBase
{
    [HttpPost("add")]
    public async Task<IActionResult> Add([FromBody] AddDeviceRequest request,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo,
        [FromServices] DevicesRepository devicesRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? email)) 
            return Unauthorized();

        if (!await usersRepo.ExistsAsync(email))
            return Conflict("User doesn't exist");

        var success = await devicesRepo.ExistsAsync(request.FcmToken)
            ? await devicesRepo.ChangeUserAsync(request.UserId, request.FcmToken)
            : await devicesRepo.AddAsync(request);

        Logger.Log(success ? $"Device for user '{email}' added successfully" : $"Failed to add device for user '{email}'");
        
        return success ? Ok() : Conflict();
    }

    [HttpPatch("update")]
    public async Task<IActionResult> Update([FromBody] UpdateDeviceRequest request,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo,
        [FromServices] DevicesRepository devicesRepo)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();
        
        if (!await usersRepo.ExistsAsync(email!))
            return Conflict("User doesn't exist");

        if (!await devicesRepo.ExistsAsync(request.OldFcmToken))
            return Conflict("Device doesn't exist");
        
        bool success = await devicesRepo.UpdateAsync(request);
        Logger.Log(success ? $"Device for user '{email}' updated successfully" : $"Failed to update device for user '{email}'");
        
        return success ? Ok() : StatusCode(500, "Failed to update device");
    }

    [HttpDelete("delete/{fcmToken}")]
    public async Task<IActionResult> Delete([FromRoute] string fcmToken,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo,
        [FromServices] DevicesRepository devicesRepo)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();
        
        if (!await usersRepo.ExistsAsync(email!))
            return Conflict("User doesn't exist");
        
        if (!await devicesRepo.ExistsAsync(fcmToken))
            return Conflict("Device doesn't exist");

        bool success = await devicesRepo.DeleteAsync(fcmToken);
        
        Logger.Log(success
            ? $"Device for user '{email}' deleted successfully"
            : $"Failed to delete device for user '{email}'");
        
        return success ? Ok() : StatusCode(500, "Failed to delete device");
    }
}