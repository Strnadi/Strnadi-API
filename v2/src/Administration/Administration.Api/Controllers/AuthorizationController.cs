using System.Collections.Immutable;
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
public class AuthorizationController(UserManager<User> users, AdminDbContext db, ILogger<AuthorizationController> logger) : ControllerBase
{
    [HttpGet("authorize"), HttpPost("authorize"), IgnoreAntiforgeryToken]
    public async Task<IActionResult> AuthorizeAsync(
        [FromQuery(Name = "client_id")] string? clientId,
        [FromQuery(Name = "response_type")] string? responseType,
        [FromQuery(Name = "redirect_uri")] string? redirectUri,
        [FromQuery] string? scope,
        [FromQuery] string? state,
        [FromQuery(Name = "code_challenge")] string? codeChallenge,
        [FromQuery(Name = "code_challenge_method")] string? codeChallengeMethod)
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
        {
            logger.LogWarning("Authorization request failed: authenticated principal has no matching user");
            return Forbid(IdentityConstants.ApplicationScheme);
        }

        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType, Claims.Name,
            Claims.Role);

        identity.SetClaim(Claims.Subject, user.Id.ToString())
            .SetClaim(Claims.Email, user.Email)
            .SetClaim(Claims.EmailVerified, user.EmailConfirmed.ToString());

        identity.SetScopes(request.GetScopes());
        identity.SetDestinations(GetDestinations);

        logger.LogInformation("Issued authorization code to user {UserId} for client {ClientId}", user.Id, clientId);
        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>Token endpoint. Exchanges an authorization code, refresh token, or project token-exchange request for an access token.</summary>
    [HttpGet("token"), HttpPost("token")]
    public async Task<IActionResult> IssueTokenAsync()
    {
        var request = HttpContext.GetOpenIddictServerRequest()!;

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var userId = result.Principal!.GetClaim(Claims.Subject)!;

            if (request.IsRefreshTokenGrantType() && !await users.IsActiveAsync(userId))
            {
                logger.LogWarning("Token refresh denied for inactive user {UserId}", userId);
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            logger.LogInformation("Issued {GrantType} token for user {UserId}", request.GrantType, userId);
            return SignIn(result.Principal!, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsTokenExchangeGrantType())
            return await ExchangeForProjectTokenAsync(request);

        logger.LogWarning("Token request failed: unsupported grant type {GrantType}", request.GrantType);
        return BadRequest(new OpenIddictResponse
        {
            Error = Errors.UnsupportedGrantType
        });
    }

    /// <summary>OIDC userinfo endpoint. Returns claims for the caller's access token.</summary>
    [HttpGet("user-info"), HttpPost("user-info")]
    public async Task<IActionResult> GetUserInfoAsync()
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        if (!result.Succeeded)
        {
            logger.LogWarning("Userinfo request failed: unauthenticated");
            return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

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

    /// <summary>Signs the caller out and ends the OpenIddict session.</summary>
    [Route("logout")]
    public async Task<IActionResult> LogoutAsync([FromQuery(Name = "redirect_uri")] string? redirectUri)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        logger.LogInformation("Ended OpenIddict session for user {UserId}", userId);

        return SignOut(
            new AuthenticationProperties
            {
                RedirectUri = redirectUri ?? "/"
            }, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> ExchangeForProjectTokenAsync(OpenIddictRequest request)
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!result.Succeeded)
        {
            logger.LogWarning("Project token exchange failed: unauthenticated");
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var userId = Guid.Parse(result.Principal!.GetClaim(Claims.Subject)!);
        var projectId = Guid.Parse(request.GetParameter("project_id")!.ToString()!);

        if (!await db.ProjectMemberships.AnyAsync(m => m.UserId == userId && m.ProjectId == projectId))
        {
            logger.LogWarning("Project token exchange denied: user {UserId} is not a member of project {ProjectId}", userId, projectId);
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var roles = await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(db.Roles.Where(r => r.ProjectId == projectId), ur => ur.RoleId, r => r.Id, (_, r) => r)
            .ToListAsync();

        var roleIds = roles.Select(r => r.Id).ToList();
        var permissions = await db.RoleClaims
            .Where(rc => roleIds.Contains(rc.RoleId) && rc.ClaimType == "permission")
            .Select(rc => rc.ClaimValue!)
            .ToListAsync();

        var identity =
            new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, userId.ToString())
            .SetClaim("project_id", projectId.ToString());
        identity.SetClaims(Claims.Role, roles.Select(r => r.Name!).ToImmutableArray());
        identity.SetClaims("permission", permissions.ToImmutableArray());
        identity.SetAudiences($"project:{projectId}");
        identity.SetDestinations(GetDestinations);

        logger.LogInformation("Issued project-scoped token for user {UserId}, project {ProjectId}", userId, projectId);
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