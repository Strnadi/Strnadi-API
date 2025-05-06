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

using System.Text.Json;
using Auth.Services;
using Email;
using Google.Apis.Auth;
using Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Shared.Extensions;
using Shared.Logging;
using Shared.Models.Database;
using Shared.Models.Requests.Auth;

namespace Auth;

[ApiController]
[Route("/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    
    private string _androidId => _configuration["Auth:Google:Android"] ?? throw new NullReferenceException();
    private string _iosId => _configuration["Auth:Google:Ios"] ?? throw new NullReferenceException();
    private string _webId => _configuration["Auth:Google:Web"] ?? throw new NullReferenceException();
    private string _webSecret => _configuration["Auth:Google:WebSecret"] ?? throw new NullReferenceException();

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

    [HttpPost("apple")]
    public async Task<IActionResult> SignInViaApple([FromForm] AppleCallbackRequest req)
    {
        AppleUserInfo? appleUser = null;
        if (!string.IsNullOrEmpty(req.User))
        {
            appleUser = JsonSerializer.Deserialize<AppleUserInfo>(req.User);
        }
        if (appleUser is null) return StatusCode(500, "Apple authorization failed");

        throw new NotImplementedException();
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

        UserModel user = (await repo.GetUserByEmailAsync(email))!;

        if (user.IsEmailVerified.HasValue && !user.IsEmailVerified.Value || !user.IsEmailVerified.HasValue)
        {
            user.IsEmailVerified = true;
        }

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
        
        var user = await repo.GetUserByEmailAsync(request.Email);
            
        if (regularRegister) 
            emailService.SendEmailVerificationAsync(user!.Email, user.Id, nickname: request.Nickname, newJwt);
        else 
            await repo.VerifyEmailAsync(request.Email);

        Logger.Log($"User '{request.Email}' signed in successfully");
        return Ok(newJwt);
    }

    [HttpGet("{userId:int}/resend-verify-email")]
    public async Task<IActionResult> ResendVerifyEmailAsync([FromRoute] int userId,
        [FromServices] JwtService jwtService,
        [FromServices] EmailService emailService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();

        if (!jwtService.TryValidateToken(jwt, out string? emailFromJwt))
            return Unauthorized();
        
        var user = await usersRepo.GetUserByIdAsync(userId);
        if (user is null)
            return Unauthorized("User not found");

        if (user.Email != emailFromJwt)
            return BadRequest("Invalid email");

        if (await usersRepo.IsEmailVerifiedAsync(user.Email))
            return StatusCode(208, "Email is already verified"); // Already reported

        Logger.Log($"Resend verification email to '{user.Email}'");
        
        string newJwt = jwtService.GenerateToken(user.Email);
        emailService.SendEmailVerificationAsync(user.Email, user.Id, nickname: null, newJwt);

        return Ok();
    }

    [HttpGet("{email}/reset-password")]
    public async Task<IActionResult> ResetPasswordAsync([FromRoute] string email,
        [FromServices] UsersRepository usersRepo,
        [FromServices] JwtService jwtService,
        [FromServices] EmailService emailService)
    {
        var user = await usersRepo.GetUserByEmailAsync(email);
        if (user is null)
            return NotFound("User not found");
        
        string jwt = jwtService.GenerateToken(email);

        emailService.SendPasswordResetMessage(email, user.Id, nickname: null, jwt);

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