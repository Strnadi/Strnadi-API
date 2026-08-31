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
using Shared.Tools;

namespace Users;

[ApiController]
[Route("users")]
public class UsersController : ControllerBase
{
    /// <summary>
    /// Lists all users. Requires a valid JWT belonging to an administrator.
    /// </summary>
    /// <returns>The array of users, 400 if the JWT is missing, 401 if it is invalid or the caller is not an administrator, or 500 on failure.</returns>
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

    /// <summary>
    /// Gets the caller's own user identifier, derived from their JWT.
    /// </summary>
    /// <returns>The caller's user id, 400 if the JWT is missing, or 401 if it is invalid or the user is not found.</returns>
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

    /// <summary>
    /// Gets a user's profile by identifier. The email address is only included when the caller is the
    /// user themselves or an administrator.
    /// </summary>
    /// <param name="userId">Identifier of the user to retrieve.</param>
    /// <returns>The user profile, or 409 if the user does not exist.</returns>
    [HttpGet("{userId:int}")]
    public async Task<IActionResult> GetById([FromRoute] int userId,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();
        User? user;
        if (string.IsNullOrEmpty(jwt))
        {
            user = await usersRepo.GetUserByIdAsync(userId);
            if (user is null)
                return Conflict("User not found");

            user.Email = null!;

            return Ok(user);
        }

        if (!jwtService.TryValidateToken(jwt, out string? emailFromJwt))
            return Unauthorized();

        user = await usersRepo.GetUserByIdAsync(userId);
        if (user is null)
            return Conflict("User not found");

        if (!await usersRepo.IsAdminAsync(emailFromJwt) && user.Email != emailFromJwt)
        {
            user.Email = null!;
        }

        return Ok(user);
    }

    /// <summary>
    /// Updates a user's profile. Requires a valid JWT belonging to the user themselves or an administrator.
    /// </summary>
    /// <param name="userId">Identifier of the user to update.</param>
    /// <param name="model">The fields to update.</param>
    /// <returns>200 on success, 400 if the JWT/caller lacks permission, 401 if the JWT is invalid, or 409 on failure.</returns>
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

        Logger.Log(updated ? $"User '{user.Email}' has been updated" : $"Failed to update user '{user.Email}'");

        return updated ? Ok() : StatusCode(409, "Failed to update user");
    }

    /// <summary>
    /// Deletes a user. Requires a valid JWT belonging to the user themselves or an administrator.
    /// </summary>
    /// <param name="userId">Identifier of the user to delete.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if it is invalid or the caller lacks permission, or 404 on failure.</returns>
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

        Logger.Log(deleted ? $"User '{user.Email}' has been deleted" : $"Failed to delete user '{user.Email}'");

        return deleted ? Ok() : StatusCode(404, "Failed to delete user");
    }

    /// <summary>
    /// Verifies a user's email address using a verification JWT, then redirects to a confirmation page.
    /// </summary>
    /// <param name="userId">Identifier of the user whose email is being verified.</param>
    /// <param name="jwt">The email-verification JWT sent to the user.</param>
    /// <returns>A permanent redirect to the verification result page.</returns>
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

        Logger.Log(verified ? $"Email verified: '{user.Email}'" : $"Failed to verify email: '{user.Email}'");

        return RedirectPermanent(linkGenerator.GenerateEmailVerificationRedirectionLink(verified));
    }

    /// <summary>
    /// Changes a user's password. Requires a valid JWT matching the target user.
    /// </summary>
    /// <param name="userId">Identifier of the user whose password is being changed.</param>
    /// <param name="request">Contains the new password.</param>
    /// <returns>200 on success, 400 if the JWT/email is invalid, 401 if the JWT is invalid or does not match the user, 404 if the user does not exist, or 500 on failure.</returns>
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

        Logger.Log(changed
            ? $"Password changed for email: '{user.Email}'"
            : $"Failed to change password for email: '{user.Email}'");

        if (!changed)
            return StatusCode(500, "Failed to change password");

        return Ok();
    }

    /// <summary>
    /// Checks whether a user exists by identifier or email address.
    /// </summary>
    /// <param name="userId">Identifier of the user to check for. Used when <paramref name="email"/> is not provided.</param>
    /// <param name="email">Email address of the user to check for. Takes precedence over <paramref name="userId"/>.</param>
    /// <returns>409 if the user exists, 200 if not, or 400 if neither <paramref name="userId"/> nor <paramref name="email"/> was provided.</returns>
    [HttpGet("exists")]
    public async Task<IActionResult> Exists([FromQuery] int? userId,
        [FromQuery] string? email,
        [FromServices] UsersRepository usersRepo)
    {
        bool exists;

        if (email is not null)
            exists = await usersRepo.ExistsAsync(email);
        else if (userId is not null)
            exists = await usersRepo.ExistsAsync(userId.Value);
        else
            return BadRequest("Email and userId was not provided");

        return exists ? Conflict("Exists") : Ok();
    }

    /// <summary>
    /// Uploads a user's profile photo. Requires a valid JWT.
    /// </summary>
    /// <param name="userId">Identifier of the user to set the profile photo for.</param>
    /// <param name="req">The profile photo contents.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if it is invalid, or 409 on failure.</returns>
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

        if (!jwtService.TryValidateToken(jwt, out _))
            return Unauthorized();

        bool success = await repo.UploadUserPhotoAsync(userId, req);

        Logger.Log(success ? $"Uploaded profile photo for user {userId}" : $"Failed to upload profile photo for user {userId}");

        return success ? Ok() : Conflict("Failed to save user photo");
    }

    /// <summary>
    /// Gets a user's profile photo.
    /// </summary>
    /// <param name="userId">Identifier of the user to get the profile photo for.</param>
    /// <returns>The profile photo, or 404 if the user has none.</returns>
    [HttpGet("{userId:int}/get-profile-photo")]
    public async Task<IActionResult> GetUserProfilePhoto([FromRoute] int userId,
        [FromServices] PhotosRepository photosRepo,
        [FromServices] JwtService jwtService)
    {
        UserProfilePhotoModel? model = await photosRepo.GetUserPhotoAsync(userId);

        return model is not null ? Ok(model) : NotFound("User doesnt have profile photo");
    }
}
