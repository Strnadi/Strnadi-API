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
using Microsoft.Extensions.Logging;
using Repository;
using Shared.Extensions;
using Shared.Logging;
using Shared.Models.Requests.Devices;

namespace Devices;

/// <summary>
/// Handles device registration and FCM token management.
/// </summary>
[ApiController]
[Route("/devices")]
public class DevicesController : ControllerBase
{
    /// <summary>
    /// Adds a device for a user, or moves an existing FCM token to the requested user.
    /// </summary>
    /// <param name="request">Device data to register.</param>
    /// <param name="jwtService">JWT validation service.</param>
    /// <param name="usersRepo">Repository used to verify the authenticated user exists.</param>
    /// <param name="devicesRepo">Repository used to create or update the device record.</param>
    /// <returns>An HTTP result indicating whether the device was added.</returns>
    [HttpPost("add")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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

    /// <summary>
    /// Updates an existing device FCM token.
    /// </summary>
    /// <param name="request">The old and new FCM token values.</param>
    /// <param name="jwtService">JWT validation service.</param>
    /// <param name="usersRepo">Repository used to verify the authenticated user exists.</param>
    /// <param name="devicesRepo">Repository used to update the device record.</param>
    /// <returns>An HTTP result indicating whether the device token was updated.</returns>
    [HttpPatch("update")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

    /// <summary>
    /// Deletes a registered device by FCM token.
    /// </summary>
    /// <param name="fcmToken">FCM token identifying the device to delete.</param>
    /// <param name="jwtService">JWT validation service.</param>
    /// <param name="usersRepo">Repository used to verify the authenticated user exists.</param>
    /// <param name="devicesRepo">Repository used to delete the device record.</param>
    /// <returns>An HTTP result indicating whether the device was deleted.</returns>
    [HttpDelete("delete/{fcmToken}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
