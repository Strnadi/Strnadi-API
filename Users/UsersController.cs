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
using Microsoft.Extensions.Logging;
using Shared.Extensions;
using Shared.Logging;
using Shared.Models.Requests;
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
        
        if (email != emailFromJwt && !await usersRepo.IsAdminAsync(email))
            return BadRequest("User does not belong to this email or is not an admin");
        
        if (!await usersRepo.ExistsAsync(email))
            return Conflict("User not found");
       
        var user = await usersRepo.GetUserByEmail(email);
        
        if (user is null)
            return StatusCode(500, "Failed to get user");

        return Ok(user);
    }

    [HttpGet("{email}/verify-email")]
    public async Task<IActionResult> VerifyEmailAsync(string email,
        [FromServices] JwtService jwtService,
        [FromServices] LinkGenerator linkGenerator,
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
        
        bool verified = await usersRepo.VerifyEmailAsync(email);
        
        Logger.Log($"Email verified: '{email}'");
        
        string redirectionPage = linkGenerator.GenerateEmailVerificationRedirectionLink(verified);
        return RedirectPermanent(redirectionPage);
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

        return Ok();
    }
}