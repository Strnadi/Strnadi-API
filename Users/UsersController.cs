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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Extensions;
using Shared.Logging;
using Shared.Models.Database;
using Shared.Models.Requests.Photos;
using Shared.Models.Requests.Users;
using Shared.Tools;

namespace Users;

/// <summary>
/// Provides user profile, account, verification, and profile photo endpoints.
/// </summary>
[ApiController]
[Route("users")]
public class UsersController : ControllerBase
{
    /// <summary>
    /// Gets all users for an administrator.
    /// </summary>
    /// <param name="jwtService">Service used to validate the current JWT.</param>
    /// <param name="usersRepo">Repository used to check administrator access and load users.</param>
    /// <returns>An HTTP result containing all users when the caller is an administrator.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(User[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
    /// Gets the user identifier for the current JWT.
    /// </summary>
    /// <param name="jwtService">Service used to validate the current JWT.</param>
    /// <param name="usersRepo">Repository used to load the current user.</param>
    /// <returns>An HTTP result containing the current user's identifier.</returns>
    [HttpGet("get-id")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
    /// Gets a user by identifier, hiding the email address when the caller is not authorized to view it.
    /// </summary>
    /// <param name="userId">Identifier of the user to load.</param>
    /// <param name="jwtService">Service used to validate an optional JWT.</param>
    /// <param name="usersRepo">Repository used to load the user and check administrator access.</param>
    /// <returns>An HTTP result containing the requested user.</returns>
    [HttpGet("{userId:int}")]
    [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
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
    /// Updates the specified user when the current JWT belongs to that user or an administrator.
    /// </summary>
    /// <param name="userId">Identifier of the user to update.</param>
    /// <param name="model">User fields to update.</param>
    /// <param name="jwtService">Service used to validate the current JWT.</param>
    /// <param name="usersRepo">Repository used to load and update the user.</param>
    /// <returns>An HTTP result indicating whether the user was updated.</returns>
    [HttpPatch("{userId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
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
    /// Deletes the specified user when the current JWT belongs to that user or an administrator.
    /// </summary>
    /// <param name="userId">Identifier of the user to delete.</param>
    /// <param name="jwtService">Service used to validate the current JWT.</param>
    /// <param name="usersRepo">Repository used to load and delete the user.</param>
    /// <returns>An HTTP result indicating whether the user was deleted.</returns>
    [HttpDelete("{userId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
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
    /// Verifies a user's email address using a verification JWT and redirects to the verification result page.
    /// </summary>
    /// <param name="userId">Identifier of the user whose email should be verified.</param>
    /// <param name="jwt">Verification JWT from the email link.</param>
    /// <param name="jwtService">Service used to validate the verification JWT.</param>
    /// <param name="linkGenerator">Service used to build the verification result redirect URL.</param>
    /// <param name="usersRepo">Repository used to load and verify the user.</param>
    /// <returns>A permanent redirect to the email verification result page, or an unauthorized response.</returns>
    [HttpGet("{userId:int}/verify-email")]
    [ProducesResponseType(StatusCodes.Status301MovedPermanently)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
    /// Changes the password for the specified user.
    /// </summary>
    /// <param name="userId">Identifier of the user whose password should be changed.</param>
    /// <param name="request">Password change request containing the new password.</param>
    /// <param name="jwtService">Service used to validate the current JWT.</param>
    /// <param name="usersRepo">Repository used to load the user and change the password.</param>
    /// <returns>An HTTP result indicating whether the password was changed.</returns>
    [HttpPatch("{userId:int}/change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
    /// Checks whether a user exists by email address or user identifier.
    /// </summary>
    /// <param name="userId">Optional user identifier to check.</param>
    /// <param name="email">Optional email address to check.</param>
    /// <param name="usersRepo">Repository used to check for a matching user.</param>
    /// <returns>A conflict result when the user exists, otherwise an OK result.</returns>
    [HttpGet("exists")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
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
    /// Uploads or replaces a user's profile photo.
    /// </summary>
    /// <param name="userId">Identifier of the user whose photo should be saved.</param>
    /// <param name="req">Profile photo payload to save.</param>
    /// <param name="repo">Repository used to save the profile photo.</param>
    /// <param name="jwtService">Service used to validate the current JWT.</param>
    /// <returns>An HTTP result indicating whether the profile photo was saved.</returns>
    [HttpPost("{userId:int}/upload-profile-photo")]
    [RequestSizeLimit(130023424)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
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
    /// <param name="userId">Identifier of the user whose photo should be loaded.</param>
    /// <param name="photosRepo">Repository used to load the profile photo.</param>
    /// <param name="jwtService">Service provided by dependency injection for this endpoint.</param>
    /// <returns>An HTTP result containing the user's profile photo when one exists.</returns>
    [HttpGet("{userId:int}/get-profile-photo")]
    [ProducesResponseType(typeof(UserProfilePhotoModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserProfilePhoto([FromRoute] int userId,
        [FromServices] PhotosRepository photosRepo,
        [FromServices] JwtService jwtService)
    {
        UserProfilePhotoModel? model = await photosRepo.GetUserPhotoAsync(userId);

        return model is not null ? Ok(model) : NotFound("User doesnt have profile photo");
    }
}
