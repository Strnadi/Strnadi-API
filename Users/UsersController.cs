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
using Microsoft.AspNetCore.Server.HttpSys;
using Shared.Extensions;
using Shared.Logging;
using Shared.Models.Requests.Photos;
using Shared.Models.Requests.Users;

namespace Users;

[ApiController]
[Route("users")]
public class UsersController : ControllerBase
{
    [HttpGet("{email}")]
    public async Task<IActionResult> Get(string email,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null) 
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? emailFromJwt))
            return Unauthorized();
        //
        // if (email != emailFromJwt && !await usersRepo.IsAdminAsync(emailFromJwt!))
        //     return BadRequest("User does not belong to this email or is not an admin");
        
        if (!await usersRepo.ExistsAsync(email))
            return Conflict("User not found");
       
        var user = await usersRepo.GetUserByEmail(email);
        
        if (user is null)
            return StatusCode(500, "Failed to get user");

        return Ok(user);
    }

    [HttpPatch("{email}")]
    public async Task<IActionResult> Update(string email, 
        [FromBody] UpdateUserModel model,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? emailFromJwt))
            return Unauthorized();
        
        if (email != emailFromJwt && !await usersRepo.IsAdminAsync(emailFromJwt!))
            return Unauthorized("User does not belong to this email nor is an administrator");
        
        bool updated = await usersRepo.UpdateAsync(email, model);
        
        return updated ? Ok() : StatusCode(409, "Failed to update user");
    }

    [HttpDelete("{email}")]
    public async Task<IActionResult> DeleteUser([FromRoute] string email,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? emailFromJwt))
            return Unauthorized();
        
        if (!await usersRepo.ExistsAsync(email))
            return NotFound("User not found");
        
        if (email != emailFromJwt && !await usersRepo.IsAdminAsync(emailFromJwt!))
            return Unauthorized("User does not belong to this email nor is an administrator");

        bool deleted = await usersRepo.DeleteAsync(email);
        
        return deleted ? Ok() : StatusCode(404, "Failed to delete user");
    }

    [HttpGet("{email}/verify-email")]
    public async Task<IActionResult> VerifyEmailAsync(string email,
        [FromQuery] string jwt,
        [FromServices] JwtService jwtService,
        [FromServices] LinkGenerator linkGenerator,
        [FromServices] UsersRepository usersRepo)
    { 
        if (!jwtService.TryValidateToken(jwt, out string? emailFromJwt))
            return Unauthorized();

        if (email != emailFromJwt)
            return RedirectPermanent(linkGenerator.GenerateEmailVerificationRedirectionLink(false));
            
        if (!await usersRepo.ExistsAsync(email))
            return RedirectPermanent(linkGenerator.GenerateEmailVerificationRedirectionLink(false));
        
        bool verified = await usersRepo.VerifyEmailAsync(email);
        
        Logger.Log($"Email verified: '{email}'");
        
        return RedirectPermanent(linkGenerator.GenerateEmailVerificationRedirectionLink(verified));
    }

    [HttpPatch("{email}/change-password")]
    public async Task<IActionResult> ChangePasswordAsync(string email,
        [FromBody] ChangePasswordRequest request,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null) 
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? emailFromJwt))
            return Unauthorized();

        if (email != emailFromJwt)
            return BadRequest("Invalid email");
            
        if (!await usersRepo.ExistsAsync(email))
            return NotFound("User not found");

        bool changed = await usersRepo.ChangePasswordAsync(email, request.NewPassword);
        
        if (!changed)
            return StatusCode(500, "Failed to change password");

        Logger.Log($"Password changed for email: '{email}'");
        
        return Ok();
    }

    [HttpGet("exists")]
    public async Task<IActionResult> Exists([FromQuery] string email,
        [FromServices] UsersRepository usersRepo)
    {
        bool exists = await usersRepo.ExistsAsync(email);

        if (exists)
        {
            return Conflict("User already exists");
        }
        else
        {
            return Ok();
        }
    }

    [HttpPost("{email}/upload-profile-photo")]
    public async Task<IActionResult> UploadUserProfilePhoto([FromRoute] string email,
        [FromBody] UserProfilePhotoModel req,
        [FromServices] PhotosRepository repo,
        [FromServices] JwtService jwtService)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? emailFromJwt))
            return Unauthorized();
        
        if (email != emailFromJwt)
            return BadRequest("Invalid email");

        bool success = await repo.UploadUserPhotoAsync(email, req);
        
        return success ? Ok() : Conflict("Failed to save user photo");
    }

    [HttpGet("{email}/get-profile-photo")]
    public async Task<IActionResult> GetUserProfilePhoto([FromRoute] string email,
        [FromServices] PhotosRepository photosRepo,
        [FromServices] JwtService jwtService)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? emailFromJwt))
            return Unauthorized();
        
        if (email != emailFromJwt)
            return BadRequest("Invalid email");

        UserProfilePhotoModel? model = await photosRepo.GetUserPhotoAsync(email);

        return model is not null ? 
            Ok(model) : 
            NotFound("User doesnt have profile photo");
    }
}