using System.Security.Claims;
using Administration.Domain.Entities;
using Administration.Infrastructure.Identity;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Administration.Api.Controllers;

[ApiController]
[Route("connect")]
public class AuthorizationController(UserManager<User> users, AdminDbContext db) : ControllerBase
{
    [HttpGet("authorize"), HttpPost("authorize"), IgnoreAntiforgeryToken]
    public async Task<IActionResult> AuthorizeAsync()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
                      ?? throw new InvalidOperationException("Cannot retrieve OpenIddict request");

        var authResult = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!authResult.Succeeded)
            return Challenge(new AuthenticationProperties
            {
                RedirectUri = Request.PathBase + Request.Path + Request.QueryString
            }, IdentityConstants.ApplicationScheme);

        var user = await users.FindByIdAsync(authResult.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (user is null)
            return Forbid(IdentityConstants.ApplicationScheme);

        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType, Claims.Name,
            Claims.Role);

        identity.SetClaim(Claims.Subject, user.Id.ToString())
            .SetClaim(Claims.Email, user.Email)
            .SetClaim(Claims.EmailVerified, user.EmailConfirmed.ToString());

        identity.SetScopes(request.GetScopes());
        identity.SetDestinations(GetDestinations);

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("token"), HttpPost("token")]
    public async Task<IActionResult> IssueTokenAsync()
    {
        var request = HttpContext.GetOpenIddictServerRequest()!;

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var userId = result.Principal!.GetClaim(Claims.Subject)!;
            
            if (request.IsRefreshTokenGrantType() && !await users.IsActiveAsync(userId))
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            
            return SignIn(result.Principal!, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsTokenExchangeGrantType())
            return await ExchangeForProjectTokenAsync(request);

        return BadRequest(new OpenIddictResponse
        {
            Error = Errors.UnsupportedGrantType
        });
    }

    [HttpGet("user-info"), HttpPost("user-info")]
    public async Task<IActionResult> GetUserInfoAsync()
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        if (!result.Succeeded)
            return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var claims = new Dictionary<string, object>
        {
            [Claims.Subject] = result.Principal!.GetClaim(Claims.Subject)!
        };

        if (result.Principal.HasScope(Scopes.Email))
        {
            claims[Claims.Email] = result.Principal.GetClaim(Claims.Email)!;
            claims[Claims.EmailVerified] = bool.Parse(result.Principal.GetClaim(Claims.EmailVerified) ?? "false");
        }

        return Ok(claims);
    }

    [Route("logout")]
    public async Task<IActionResult> LogoutAsync()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

        return SignOut(
            new AuthenticationProperties
            {
                RedirectUri = "/"
            }, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> ExchangeForProjectTokenAsync(OpenIddictRequest request)
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!result.Succeeded)
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var userId = Guid.Parse(result.Principal!.GetClaim(Claims.Subject)!);
        var projectId = Guid.Parse(request.GetParameter("project_id")!.ToString()!);
        
        if (!await db.ProjectMemberships.AnyAsync(m => m.UserId == userId && m.ProjectId == projectId))
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var identity =
            new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, userId.ToString())
            .SetClaim("project_id", projectId.ToString());
        identity.SetAudiences($"project:{projectId}");
        identity.SetDestinations(GetDestinations);
        
        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        switch (claim.Type)
        {
            case Claims.Subject:
                yield return Destinations.AccessToken;
                yield return Destinations.IdentityToken;
                yield break;

            case Claims.Email:
            case Claims.EmailVerified:
                yield return Destinations.IdentityToken;
                if (claim.Subject!.HasScope(Scopes.Email))
                    yield return Destinations.AccessToken;
                yield break;

            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }
}