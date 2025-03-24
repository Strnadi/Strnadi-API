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
using Auth.Models;
using Auth.Services;
using Email;
using Repository;
using Microsoft.AspNetCore.Mvc;
using Shared.Extensions;
using Shared.Models.Requests.Users;

namespace Auth;

[ApiController]
[Route("/auth")]
public class AuthController : ControllerBase
{
    [HttpGet("verify-jwt")]
    public IActionResult VerifyJwt([FromServices] JwtService jwtService)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        return Ok();
    }

    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository repo)
    {
        if (!await repo.ExistsAsync(request.Email))
            return Conflict("User doesn't exist");
        
        bool authorized = await repo.AuthorizeAsync(request.Email, request.Password);
        
        if (!authorized)
            return Unauthorized();
        
        string jwt = jwtService.GenerateToken(request.Email);
        return Ok(jwt);
    }
    
    [HttpPost("sign-up")]
    public async Task<IActionResult> SignUpAsync([FromBody] SignUpRequest request,
        [FromServices] EmailService emailService,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository repo)
    {
        bool exists = await repo.ExistsAsync(request.Email);

        if (exists)
            return Conflict("User already exists");

        bool created = await repo.CreateUserAsync(request);
        
        if (!created)
            return Conflict("Failed to create user");
        
        string jwt = jwtService.GenerateToken(request.Email);
        emailService.SendEmailVerificationAsync(request.Email, nickname: request.Nickname, jwt, HttpContext);
        
        return Ok(jwt);
    }

    [HttpGet("{email}/resend-verify-email")]
    public async Task<IActionResult> ResendVerifyEmailAsync([FromRoute] string email,
        [FromServices] JwtService jwtService,
        [FromServices] EmailService emailService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();

        if (!jwtService.TryValidateToken(jwt, out string? emailFromJwt))
            return Unauthorized();

        if (email != emailFromJwt)
            return BadRequest("Invalid email");

        if (!await usersRepo.IsEmailVerifiedAsync(email))
            return StatusCode(208, "Email is already verified"); // Already reported

        string newJwt = jwtService.GenerateToken(email);
        emailService.SendEmailVerificationAsync(email, nickname: null, newJwt, HttpContext);

        return Ok();
    }
}