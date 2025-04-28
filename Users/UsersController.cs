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
using Email;
using Repository;
using Microsoft.AspNetCore.Mvc;
using Shared.Extensions;
using Shared.Logging;
using Shared.Models.Database;
using Shared.Models.Requests.Photos;
using Shared.Models.Requests.Users;

namespace Users;

[ApiController]
[Route("users")]
public class UsersController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();
        
        if (!await usersRepo.IsAdminAsync(email!))
            return Unauthorized("User is not an administrator");

        var users = await usersRepo.GetUsers();

        return users is not null ? Ok(users) : StatusCode(500);
    }

    [HttpGet("get-id")]
    public async Task<IActionResult> GetId([FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();
        
        var user = await usersRepo.GetUserByEmailAsync(email!);
        if (user is null)
            return Unauthorized("User not found");

        return Ok(user.Id);
    }
    
    [HttpGet("{userId:int}")]
    public async Task<IActionResult> GetById([FromRoute] int userId,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();
        UserModel user;
        if (jwt is null)
        {
            user = await usersRepo.GetUserByIdAsync(userId);
            if (user is null)
                return Conflict("User not found");
            user.Email = null;

            return Ok(user);
        }

        if (!jwtService.TryValidateToken(jwt, out string? emailFromJwt))
            return Unauthorized();

        user = await usersRepo.GetUserByIdAsync(userId);
        if (user is null)
            return Conflict("User not found");

        if (!await usersRepo.IsAdminAsync(emailFromJwt!))
            user.Email = null!;

        return Ok(user);
    }

    [HttpPatch("{userId:int}")]
    public async Task<IActionResult> Update([FromRoute] int userId,
        [FromBody] UpdateUserModel model,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string? emailFromJwt))
            return Unauthorized();

        var user = await usersRepo.GetUserByIdAsync(userId);
        if (user is null)
            return Unauthorized("User not found");

        if (user.Email != emailFromJwt && !user.IsAdmin)
            return BadRequest("User does not belong to this email or is not an admin");

        bool updated = await usersRepo.UpdateAsync(user.Email, model);

        return updated ? Ok() : StatusCode(409, "Failed to update user");
    }

    [HttpDelete("{userId:int}")]
    public async Task<IActionResult> DeleteUser([FromRoute] int userId,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string? emailFromJwt))
            return Unauthorized();

        var user = await usersRepo.GetUserByIdAsync(userId);
        if (user is null)
            return Unauthorized("User not found");

        if (user.Email != emailFromJwt && !user.IsAdmin)
            return Unauthorized("User does not belong to this email nor is an administrator");

        bool deleted = await usersRepo.DeleteAsync(user.Email);

        return deleted ? Ok() : StatusCode(404, "Failed to delete user");
    }

    [HttpGet("{userId:int}/verify-email")]
    public async Task<IActionResult> VerifyEmailAsync([FromRoute] int userId,
        [FromQuery] string jwt,
        [FromServices] JwtService jwtService,
        [FromServices] LinkGenerator linkGenerator,
        [FromServices] UsersRepository usersRepo)
    {
        if (!jwtService.TryValidateToken(jwt, out string? emailFromJwt))
            return Unauthorized();

        var user = await usersRepo.GetUserByIdAsync(userId);
        if (user!.Email != emailFromJwt)
            return RedirectPermanent(linkGenerator.GenerateEmailVerificationRedirectionLink(false));

        if (!await usersRepo.ExistsAsync(user.Email))
            return RedirectPermanent(linkGenerator.GenerateEmailVerificationRedirectionLink(false));

        bool verified = await usersRepo.VerifyEmailAsync(userId);

        Logger.Log($"Email verified: '{user.Email}'");

        return RedirectPermanent(linkGenerator.GenerateEmailVerificationRedirectionLink(verified));
    }

    [HttpPatch("{userId:int}/change-password")]
    public async Task<IActionResult> ChangePasswordAsync(int userId,
        [FromBody] ChangePasswordRequest request,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string? emailFromJwt))
            return Unauthorized();

        var user = await usersRepo.GetUserByIdAsync(userId);
        if (user is null)
            return Unauthorized("User not found");

        if (user.Email != emailFromJwt)
            return BadRequest("Invalid email");

        if (!await usersRepo.ExistsAsync(user.Email))
            return NotFound("User not found");

        bool changed = await usersRepo.ChangePasswordAsync(user.Email, request.NewPassword);

        if (!changed)
            return StatusCode(500, "Failed to change password");

        Logger.Log($"Password changed for email: '{user.Email}'");

        return Ok();
    }

    [HttpGet("exists")]
    public async Task<IActionResult> Exists([FromQuery] int userId,
        [FromServices] UsersRepository usersRepo)
    {
        var user = await usersRepo.GetUserByIdAsync(userId);
        if (user is null)
            return NotFound("User not found");

        bool exists = await usersRepo.ExistsAsync(user.Email);

        if (exists)
        {
            return Conflict("User already exists");
        }
        else
        {
            return Ok();
        }
    }

    [HttpPost("{userId:int}/upload-profile-photo")]
    [RequestSizeLimit(130023424)]
    public async Task<IActionResult> UploadUserProfilePhoto([FromRoute] int userId,
        [FromBody] UserProfilePhotoModel req,
        [FromServices] PhotosRepository repo,
        [FromServices] JwtService jwtService)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string? emailFromJwt))
            return Unauthorized();

        bool success = await repo.UploadUserPhotoAsync(userId, req);

        return success ? Ok() : Conflict("Failed to save user photo");
    }

    [HttpGet("{userId:int}/get-profile-photo")]
    public async Task<IActionResult> GetUserProfilePhoto([FromRoute] int userId,
        [FromServices] PhotosRepository photosRepo,
        [FromServices] JwtService jwtService)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out _))
            return Unauthorized();

        UserProfilePhotoModel? model = await photosRepo.GetUserPhotoAsync(userId);

        return model is not null ? Ok(model) : NotFound("User doesnt have profile photo");
    }
}