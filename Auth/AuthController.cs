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
using Google.Apis.Auth;
using Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Shared.Extensions;
using Shared.Logging;
using Shared.Models.Requests.Auth;

namespace Auth;

[ApiController]
[Route("/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    
    private string _androidId => _configuration["Auth:Google:Android"];
    private string _iosId => _configuration["Auth:Google:Ios"];
    private string _webId => _configuration["Auth:Google:Web"];
    private string _webSecret => _configuration["Auth:Google:WebSecret"];

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    [HttpGet("verify-jwt")]
    public async Task<IActionResult> VerifyJwt([FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        string? email = jwtService.GetEmail(jwt);
        
        if (email is null)
            return BadRequest("No email provided");

        return await usersRepo.IsEmailVerifiedAsync(email) ? Ok() : StatusCode(403);
    }

    [HttpPost("sign-up-google")]
    public async Task<IActionResult> SignUpViaGoogle([FromBody] GoogleAuthRequest req,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository repo)
    {
        var payload = await ValidateGoogleIdTokenAsync(req.IdToken);
        if (payload is null)
            return Unauthorized("Invalid ID token");
        
        string email = payload.Email;
        if (await repo.ExistsAsync(email))
            return Conflict("User already exists");
        
        string jwt = jwtService.GenerateToken(email);
        return Ok(new { jwt, firstName = payload.GivenName, lastName = payload.FamilyName});
    }
    
    [HttpPost("login-google")]
    public async Task<IActionResult> LoginViaGoogle([FromBody] GoogleAuthRequest req,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository repo)
    {
        var payload = await ValidateGoogleIdTokenAsync(req.IdToken);
        if (payload is null)
            return Unauthorized("Invalid ID token");

        string email = payload.Email;
        if (!await repo.ExistsAsync(email))
            return Conflict("User doesn't exist");
        
        string jwt = jwtService.GenerateToken(email);
        Logger.Log($"User '{email}' logged in successfully via google'");
        
        return Ok(jwt);
    }

    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository repo)
    {
        string email = request.Email;

        if (!await repo.ExistsAsync(email))
            return Conflict("User doesn't exist");

        bool authorized = await repo.AuthorizeAsync(email, request.Password);

        if (!authorized)
            return Unauthorized();
        
        Logger.Log($"User '{email}' logged in successfully");
        // return await repo.IsEmailVerifiedAsync(email) ?
        //     Ok(jwtService.GenerateRegularToken(email)) : 
        //     StatusCode(403, jwtService.GenerateLimitedToken(email));
        return Ok(jwtService.GenerateToken(email));
    }

    [HttpPost("sign-up")]
    public async Task<IActionResult> SignUpAsync([FromBody] SignUpRequest request,
        [FromServices] EmailService emailService,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository repo)
    {
        string? receivedJwt = this.GetJwt();
        bool regularRegister = receivedJwt is null && request.Password is not null;
        
        bool exists = await repo.ExistsAsync(request.Email);

        if (exists)
            return Conflict("User already exists");

        bool created = await repo.CreateUserAsync(request, regularRegister);
        
        if (!created)
            return Conflict("Failed to create user");

        string newJwt = jwtService.GenerateToken(request.Email);
        
        if (regularRegister) 
            emailService.SendEmailVerificationAsync(request.Email, nickname: request.Nickname, newJwt);
        else 
            await repo.VerifyEmailAsync(request.Email);

        Logger.Log($"User '{request.Email}' signed in successfully");
        
        return Ok(newJwt);
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

        if (await usersRepo.IsEmailVerifiedAsync(email))
            return StatusCode(208, "Email is already verified"); // Already reported

        Logger.Log($"Resend verification email to '{email}'");
        
        string newJwt = jwtService.GenerateToken(email);
        emailService.SendEmailVerificationAsync(email, nickname: null, newJwt);

        return Ok();
    }

    [HttpGet("{email}/reset-password")]
    public async Task<IActionResult> ResetPasswordAsync([FromRoute] string email,
        [FromServices] UsersRepository usersRepo,
        [FromServices] JwtService jwtService,
        [FromServices] EmailService emailService)
    {
        if (!await usersRepo.ExistsAsync(email))
            return NotFound("User not found");
        
        string jwt = jwtService.GenerateToken(email);

        emailService.SendPasswordResetMessage(email, nickname: null, jwt);

        return Ok();
    }

    private async Task<GoogleJsonWebSignature.Payload?> ValidateGoogleIdTokenAsync(string idToken)
    {
        try
        {
            return await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [ _androidId, _iosId, _webId ]
            });
        }
        catch (Exception e)
        {
            Logger.Log($"Failed to validate google id token: {e.Message}", LogLevel.Error);
            return null;
        }
    }
}