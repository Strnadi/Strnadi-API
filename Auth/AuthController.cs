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
using Google.Apis.Auth;
using Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Shared.Extensions;
using Shared.Logging;
using Shared.Models.Database;
using Shared.Models.Requests.Auth;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using LogLevel = Shared.Logging.LogLevel;

namespace Auth;

/// <summary>
/// Provides authentication endpoints for JWT, password, Google, and Apple sign-in flows.
/// </summary>
[ApiController]
[Route("/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    private string _androidId => _configuration["Auth:Google:Android"] ?? throw new NullReferenceException();
    private string _iosId => _configuration["Auth:Google:Ios"] ?? throw new NullReferenceException();
    private string _webId => _configuration["Auth:Google:Web"] ?? throw new NullReferenceException();
    private string _webSecret => _configuration["Auth:Google:WebSecret"] ?? throw new NullReferenceException();
    private string _appleClientId => _configuration["Auth:Apple:ClientId"] ?? throw new NullReferenceException();
    private string _appleClientIdWeb => _configuration["Auth:Apple:ClientIdWeb"] ?? throw new NullReferenceException();

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="configuration">Application configuration containing authentication settings.</param>
    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Verifies that the current JWT contains an email address for a verified user.
    /// </summary>
    /// <param name="jwtService">Service used to read the JWT email claim.</param>
    /// <param name="usersRepo">Repository used to check email verification state.</param>
    /// <returns>An HTTP result indicating whether the JWT belongs to a verified user.</returns>
    [HttpGet("verify-jwt")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

    /// <summary>
    /// Renews a valid JWT for an existing user.
    /// </summary>
    /// <param name="jwtService">Service used to validate and generate JWT values.</param>
    /// <param name="usersRepo">Repository used to check that the JWT user exists.</param>
    /// <returns>An HTTP result containing a renewed JWT when the current token is valid.</returns>
    [HttpGet("renew-jwt")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RenewJwt([FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        string? email = jwtService.GetEmail(jwt);
        if (email is null)
            return BadRequest("Invalid JWT provided");

        if (!await usersRepo.ExistsAsync(email))
            return Conflict("User does not exists");

        string newJwt = jwtService.GenerateToken(email);
        return Ok(newJwt);
    }
    
    /// <summary>
    /// Starts a new account sign-up using a Google ID token.
    /// </summary>
    /// <param name="req">Google authentication request containing the ID token.</param>
    /// <param name="jwtService">Service used to generate an application JWT.</param>
    /// <param name="repo">Repository used to check existing users.</param>
    /// <returns>An HTTP result containing a JWT and Google profile names for a new user.</returns>
    [HttpPost("sign-up-google")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
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

        Logger.Log($"User '{email}' signed up successfully via Google");

        return Ok(new { jwt, firstName = payload.GivenName, lastName = payload.FamilyName });
    }

    /// <summary>
    /// Logs an existing user in using a Google ID token.
    /// </summary>
    /// <param name="req">Google authentication request containing the ID token.</param>
    /// <param name="jwtService">Service used to generate an application JWT.</param>
    /// <param name="repo">Repository used to load the matching user.</param>
    /// <returns>An HTTP result containing a JWT for an existing user.</returns>
    [HttpPost("login-google")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
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

        User user = (await repo.GetUserByEmailAsync(email))!;

        if (user.IsEmailVerified.HasValue && !user.IsEmailVerified.Value || !user.IsEmailVerified.HasValue)
        {
            user.IsEmailVerified = true;
        }

        string jwt = jwtService.GenerateToken(email);
        Logger.Log($"User '{email}' logged in successfully via google'");

        return Ok(jwt);
    }

    /// <summary>
    /// Handles Google authentication for linking, login, or sign-up discovery.
    /// </summary>
    /// <param name="req">Google authentication request containing the ID token.</param>
    /// <param name="jwtService">Service used to validate existing auth JWTs and generate new JWTs.</param>
    /// <param name="repo">Repository used to load and update Google user mappings.</param>
    /// <returns>An HTTP result describing whether the Google user exists or was linked.</returns>
    [HttpPost("google")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GoogleAuth([FromBody] GoogleAuthRequest req,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository repo)
    {
        var payload = await ValidateGoogleIdTokenAsync(req.IdToken);
        if (payload is null)
            return Unauthorized("Invalid ID token");

        string googleId = payload.Subject;
        
        User? user = await repo.GetUserByGoogleId(googleId);

        string? authJwt = this.GetJwt();
        if (authJwt is not null)
        {
            if (!jwtService.TryValidateToken(authJwt, out string? userEmail))
                return Unauthorized("Invalid auth JWT");

            await repo.AddGoogleIdAsync(email: userEmail, googleId: req.IdToken);

            return Ok();
        }
        
        if (user is not null)
        {
            // If email is not marked as verified yet
            if (user.IsEmailVerified.HasValue && !user.IsEmailVerified.Value || !user.IsEmailVerified.HasValue)
            {
                user.IsEmailVerified = true;
            }

            string jwt = jwtService.GenerateToken(user.Email);
            Logger.Log($"User '{user.Email}' logged in successfully via Google");

            return Ok(new { Exists = true, jwt });
        }
        else
        {
            string jwt = jwtService.GenerateToken(payload.Email);
            Logger.Log($"User '{payload.Email}' signed up successfully via Google");

            if (await repo.ExistsAsync(payload.Email))
            {
                await repo.AddGoogleIdAsync(payload.Email, payload.Subject);
                return Ok(new
                {
                    Exists = true,
                    Jwt = jwt,
                });
            }

            return Ok(new
            {
                Exists = false,
                Jwt = jwt,
                FirstName = payload.GivenName,
                FastName = payload.FamilyName,
                GoogleId = googleId,
            });
        }
    }

    /// <summary>
    /// Handles Apple authentication for linking, login, or sign-up discovery.
    /// </summary>
    /// <param name="req">Apple authentication request containing the ID token and optional user details.</param>
    /// <param name="jwtService">Service used to validate existing auth JWTs and generate new JWTs.</param>
    /// <param name="repo">Repository used to load and update Apple user mappings.</param>
    /// <returns>An HTTP result containing login or first-time Apple sign-in details.</returns>
    [HttpPost("apple")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginViaApple([FromBody] AppleAuthRequest req,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository repo)
    {
        var jwtToken = await ValidateAppleIdTokenAsync(req.IdToken);
        Logger.Log($"The ID token is : {req.IdToken}");
        if (jwtToken is null)
        {
            return Unauthorized("Invalid ID token");
        }

        string? authJwt = this.GetJwt();
        if (authJwt is not null)
        {
            if (!jwtService.TryValidateToken(authJwt, out string userEmail))
                return Unauthorized("Invalid auth JWT");

            if (req.UserIdentifier is null)
                return BadRequest("UserIdentifier is null");

            await repo.AddAppleIdAsync(email: userEmail, appleId: req.UserIdentifier);

            return Ok();
        }

        string? appleId = req.UserIdentifier;
        if (appleId is null) return BadRequest("UserIdentifier is null");
        bool exists = await repo.ExistsAppleAsync(appleId);
        string? email = jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;

        Logger.Log($"Apple login attempt. Exists: {exists}, Email: {email}, AppleId: {appleId}", LogLevel.Information);

        if (!exists)
        {
            if (string.IsNullOrEmpty(email))
            {
                return BadRequest("Email is required for first-time Apple sign-in");
            }

            // Treat as first‑time Apple sign‑in (sign‑up)
            string jwt = jwtService.GenerateToken(email);
            Logger.Log($"User '{email}' sign up via Apple jwt sent successfully");

            if (await repo.ExistsAsync(email))
            {
                await repo.AddAppleIdAsync(email, appleId);

                return Ok(new
                {
                    jwt,
                    exists = true,
                    firstName = req.GivenName,
                    lastName = req.FamilyName,
                    email = jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value,
                    appleid = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                });
            }
            else
            {
                return Ok(new
                {
                    jwt,
                    exists = false,
                    firstName = req.GivenName,
                    lastName = req.FamilyName,
                    email = jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value,
                    appleid = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                });
            }
        }
        else
        {
            if (string.IsNullOrEmpty(email))
            {
                User user = (await repo.GetUserByAppleIdAsync(appleId))!;

                if (user.IsEmailVerified.HasValue && !user.IsEmailVerified.Value || !user.IsEmailVerified.HasValue)
                {
                    user.IsEmailVerified = true;
                }

                string jwt = jwtService.GenerateToken(user.Email);
                Logger.Log($"User '{user.Email}' logged in successfully via Apple");

                return Ok(new { jwt });
            }
            else
            {
                User user = (await repo.GetUserByEmailAsync(email))!;
                
                await repo.AddAppleIdAsync(email, appleId);

                if (user.IsEmailVerified.HasValue && !user.IsEmailVerified.Value || !user.IsEmailVerified.HasValue)
                {
                    user.IsEmailVerified = true;
                }

                string jwt = jwtService.GenerateToken(user.Email);
                Logger.Log($"User '{user.Email}' logged in successfully via Apple");
                return Ok(new { jwt });
            }
        }
    }

    /// <summary>
    /// Receives the web Apple callback and redirects the browser back to the provided state return URL.
    /// </summary>
    /// <param name="user">Apple user payload posted by Apple, when provided.</param>
    /// <param name="state">State value containing the return URL.</param>
    /// <param name="idToken">Apple ID token posted by Apple.</param>
    /// <returns>A redirect to the return URL when state is provided, otherwise a bad request response.</returns>
    [HttpPost("apple-callback")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult AppleCallback(
        [FromForm] string? user,
        [FromForm] string? state,
        [FromForm(Name = "id_token")] string? idToken)
    {
        Logger.Log($"Got user: {user} with state: {state} and id_token: {idToken}");

        if (state is not null)
        {
            var returnUrl = state.Split("|")[0];
            return Redirect(new Uri($"{returnUrl}#user={user}&id_token={idToken}").AbsoluteUri);
        }
        else
        {
            return BadRequest();
        }
    }
    
    /// <summary>
    /// Receives the app Apple callback and redirects into the Android application intent URL.
    /// </summary>
    /// <param name="user">Apple user payload posted by Apple, when provided.</param>
    /// <param name="state">State value posted by Apple, when provided.</param>
    /// <param name="idToken">Apple ID token posted by Apple, when provided.</param>
    /// <param name="code">Apple authorization code posted by Apple, when provided.</param>
    /// <returns>A redirect to the Android intent callback URL.</returns>
    [HttpPost("apple/callback")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult AppleAppCallback(
        [FromForm] string? user,
        [FromForm] string? state,
        [FromForm(Name = "id_token")] string? idToken,
        [FromForm(Name = "code")] string? code)
    {
        Logger.Log($"[Android] Apple callback: user={user}, state={state}, id_token={(idToken != null ? "<present>" : "<null>")}, code={(code != null ? "<present>" : "<null>")}", LogLevel.Information);

        // Resolve target Android package id (prefer configuration, fallback to default)
        var androidPackage = _configuration["Auth:Android:Package"] ?? "com.delta.strnadi";

        // Build query string to forward to the app via the intent deep-link
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(code)) parts.Add($"code={Uri.EscapeDataString(code)}");
        if (!string.IsNullOrEmpty(state)) parts.Add($"state={Uri.EscapeDataString(state)}");
        if (!string.IsNullOrEmpty(idToken)) parts.Add($"id_token={Uri.EscapeDataString(idToken)}");
        if (!string.IsNullOrEmpty(user)) parts.Add($"user={Uri.EscapeDataString(user)}");
        var query = parts.Count > 0 ? "?" + string.Join("&", parts) : string.Empty;

        // Redirect back into the Android app via intent:// deep link
        var intentUrl = $"intent://callback{query}#Intent;scheme=signinwithapple;package={androidPackage};end";
        return Redirect(intentUrl);
    }

    /// <summary>
    /// Checks whether a user has an Apple identifier linked.
    /// </summary>
    /// <param name="userId">Identifier of the user to inspect.</param>
    /// <param name="users">Repository used to load the user.</param>
    /// <returns>An HTTP result indicating whether the user has an Apple identifier.</returns>
    [HttpGet("has-apple-id")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> HasAppleId([FromQuery] int userId, [FromServices] UsersRepository users)
    {
        var has = (await users.GetUserByIdAsync(userId))?.AppleId is not null;
        return has ? Ok() : Conflict();
    }

    /// <summary>
    /// Checks whether a user has a Google identifier linked.
    /// </summary>
    /// <param name="userId">Identifier of the user to inspect.</param>
    /// <param name="users">Repository used to load the user.</param>
    /// <returns>An HTTP result indicating whether the user has a Google identifier.</returns>
    [HttpGet("has-google-id")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> HasGoogleId([FromQuery] int userId, [FromServices] UsersRepository users)
    {
        var has = (await users.GetUserByIdAsync(userId))?.GoogleId is not null;
        return has ? Ok() : Conflict();
    }

    /// <summary>
    /// Logs an existing user in with email and password credentials.
    /// </summary>
    /// <param name="request">Login request containing email and password.</param>
    /// <param name="jwtService">Service used to generate an application JWT.</param>
    /// <param name="repo">Repository used to check and authorize the user.</param>
    /// <returns>An HTTP result containing a JWT when credentials are valid.</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
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

    /// <summary>
    /// Creates a user account and sends or applies email verification based on the sign-up flow.
    /// </summary>
    /// <param name="request">Sign-up request containing user profile and credential details.</param>
    /// <param name="emailService">Service used to send verification email messages.</param>
    /// <param name="jwtService">Service used to generate an application JWT.</param>
    /// <param name="repo">Repository used to create and verify the user.</param>
    /// <returns>An HTTP result containing a JWT when the account is created.</returns>
    [HttpPost("sign-up")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
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
        
        if (request.AppleId is not null && request.AppleId != "")
            await repo.AddAppleIdAsync(request.Email, request.AppleId);

        if (regularRegister)
            emailService.SendEmailVerificationAsync(user!.Email, user.Id, nickname: request.Nickname, newJwt);
        else
            await repo.VerifyEmailAsync(request.Email);

        Logger.Log($"User '{request.Email}' signed up successfully");
        return Ok(newJwt);
    }

    /// <summary>
    /// Sends a new email verification message for the specified user.
    /// </summary>
    /// <param name="userId">Identifier of the user requesting another verification email.</param>
    /// <param name="jwtService">Service used to validate the current JWT and generate a verification JWT.</param>
    /// <param name="emailService">Service used to send the verification email message.</param>
    /// <param name="usersRepo">Repository used to load and inspect the user.</param>
    /// <returns>An HTTP result indicating whether the verification email was sent.</returns>
    [HttpGet("{userId:int}/resend-verify-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status208AlreadyReported)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
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

    /// <summary>
    /// Sends a password reset message to the user with the specified email address.
    /// </summary>
    /// <param name="email">Email address of the user requesting a password reset.</param>
    /// <param name="usersRepo">Repository used to load the user.</param>
    /// <param name="jwtService">Service used to generate a password reset JWT.</param>
    /// <param name="emailService">Service used to send the password reset message.</param>
    /// <returns>An HTTP result indicating whether a password reset message was sent.</returns>
    [HttpGet("{email}/reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
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
            Logger.Log($"Failed to validate google id token: {e}", LogLevel.Error);
            return null;
        }
    }

    private async Task<JwtSecurityToken?> ValidateAppleIdTokenAsync(string idToken)
    {
        try
        {
            // Fetch Apple's OpenID configuration (keys are rotated; cache in production)
            var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                "https://appleid.apple.com/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever());

            var oidcConfig = await configurationManager.GetConfigurationAsync();
            var validationParameters = new TokenValidationParameters
            {
                ValidIssuer         = "https://appleid.apple.com",
                ValidAudiences      = [_appleClientId, _appleClientIdWeb],
                IssuerSigningKeys   = oidcConfig.SigningKeys,
                ValidateLifetime    = true
            };

            var handler = new JwtSecurityTokenHandler();
            handler.InboundClaimTypeMap.Clear(); // keep original claim names
            _ = handler.ValidateToken(idToken, validationParameters, out SecurityToken validatedToken);

            return (JwtSecurityToken)validatedToken;
        }
        catch (Exception e)
        {
            Logger.Log($"Failed to validate Apple ID token: {e}", LogLevel.Error);
            return null;
        }
    }
}
