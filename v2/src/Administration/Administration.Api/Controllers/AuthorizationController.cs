using System.Collections.Immutable;
using System.Security.Claims;
using Administration.Domain.Entities;
using Administration.Domain.Persistence.Repositories;
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
public class AuthorizationController(
    UserManager<User> users,
    AdminDbContext db,
    IUserPermissionsRepository permissions,
    ILogger<AuthorizationController> logger) : ControllerBase
{
    private static readonly string[] SupportedLanguages = ["cs", "en", "de"];

    [HttpGet("authorize"), HttpPost("authorize"), IgnoreAntiforgeryToken]
    public async Task<IActionResult> AuthorizeAsync(
        [FromQuery(Name = "client_id")] string? clientId,
        [FromQuery(Name = "response_type")] string? responseType,
        [FromQuery(Name = "redirect_uri")] string? redirectUri,
        [FromQuery] string? scope,
        [FromQuery] string? state,
        [FromQuery(Name = "code_challenge")] string? codeChallenge,
        [FromQuery(Name = "code_challenge_method")] string? codeChallengeMethod,
        [FromQuery(Name = "preferred_language")] string? preferredLanguage)
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

        if (preferredLanguage is not null && SupportedLanguages.Contains(preferredLanguage) &&
            user.PreferredLanguage != preferredLanguage)
        {
            user.PreferredLanguage = preferredLanguage;
            await users.UpdateAsync(user);
            logger.LogInformation("Updated preferred language for user {UserId} to {PreferredLanguage}", user.Id, preferredLanguage);
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

        var hasMembership = await db.ProjectMemberships.AnyAsync(m => m.UserId == userId && m.ProjectId == projectId);
        if (!hasMembership)
        {
            // A global role (Role.ProjectId == null) grants access to every project without an
            // explicit join - auto-provision the membership row so "holds a project token implies
            // ProjectMembership" stays true for anything downstream that relies on it.
            var isGlobalRoleHolder = await db.UserRoles
                .Where(ur => ur.UserId == userId)
                .Join(db.Roles.Where(r => r.ProjectId == null), ur => ur.RoleId, r => r.Id, (_, _) => 1)
                .AnyAsync();

            if (!isGlobalRoleHolder)
            {
                logger.LogWarning("Project token exchange denied: user {UserId} is not a member of project {ProjectId}", userId, projectId);
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            db.ProjectMemberships.Add(new ProjectMembership { Id = Guid.CreateVersion7(), UserId = userId, ProjectId = projectId });
            await db.SaveChangesAsync();
            logger.LogInformation("Auto-provisioned project membership for global-role holder {UserId} in project {ProjectId}", userId, projectId);
        }

        var hasOutstandingConsent = await permissions.HasOutstandingConsentAsync(userId, projectId);
        if (hasOutstandingConsent)
            logger.LogInformation(
                "User {UserId} has outstanding consent for project {ProjectId}; issuing token without roles/permissions",
                userId, projectId);

        // A global role (ProjectId == null) is a wildcard - its claims flow into every project's token.
        var roles = hasOutstandingConsent
            ? []
            : await db.UserRoles
                .Where(ur => ur.UserId == userId)
                .Join(db.Roles.Where(r => r.ProjectId == projectId || r.ProjectId == null), ur => ur.RoleId, r => r.Id, (_, r) => r)
                .ToListAsync();

        var roleIds = roles.Select(r => r.Id).ToList();
        var permissionClaims = roleIds.Count == 0
            ? []
            : await db.RoleClaims
                .Where(rc => roleIds.Contains(rc.RoleId) && rc.ClaimType == "permission")
                .Select(rc => rc.ClaimValue!)
                .ToListAsync();

        var identity =
            new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, userId.ToString())
            .SetClaim("project_id", projectId.ToString());
        identity.SetClaims(Claims.Role, roles.Select(r => r.Name!).ToImmutableArray());
        identity.SetClaims("permission", permissionClaims.ToImmutableArray());
        // "tenant-api" (the shared OpenIddict client every Tenant.Api deployment introspects as)
        // must itself be one of the audiences, or OpenIddict's introspection endpoint rejects the
        // request with "issued to a different client or for another resource server" - it only
        // lets a client introspect a token if that client is either the original token recipient
        // (here: "strnadi-app") or listed among the token's audiences. The project-scoped
        // "project:{projectId}" audience is still what Tenant.Api's own AddValidation() checks
        // locally to reject tokens for the wrong project - this doesn't weaken that.
        identity.SetAudiences("tenant-api", $"project:{projectId}");
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