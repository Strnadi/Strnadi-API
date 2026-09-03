using System.Security.Claims;
using Administration.Api.Auth;
using Administration.Domain.Entities;
using Administration.Domain.Exceptions;
using Administration.Application.Auth;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Administration.Api.Controllers;

// The single place that mints access/refresh tokens for the platform: email+password
// (password grant), and Google/Apple sign-in (custom grants — the client already validated
// the user with the native SDK and just hands us the ID token to exchange). REST endpoints
// under /auth only ever mutate accounts; they never issue tokens themselves.
[ApiController]
[Route("connect/token")]
public class TokenController(AuthService authService) : ControllerBase
{
    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Exchange(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict request cannot be retrieved.");

        try
        {
            User user;

            if (request.IsPasswordGrantType())
            {
                user = await authService.ValidateCredentialsAsync(request.Username!, request.Password!, cancellationToken);
            }
            else if (request.GrantType == AuthConstants.GoogleGrantType)
            {
                var idToken = request.GetParameter("id_token")?.ToString()
                    ?? throw new UnauthorizedException("Missing id_token parameter");
                user = await authService.ValidateGoogleAsync(idToken, cancellationToken);
            }
            else if (request.GrantType == AuthConstants.AppleGrantType)
            {
                var idToken = request.GetParameter("id_token")?.ToString()
                    ?? throw new UnauthorizedException("Missing id_token parameter");
                user = await authService.ValidateAppleAsync(idToken, cancellationToken);
            }
            else if (request.IsRefreshTokenGrantType())
            {
                var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                var subject = result.Principal?.GetClaim(OpenIddictConstants.Claims.Subject);
                if (subject is null || !int.TryParse(subject, out var userId))
                    throw new UnauthorizedException("Invalid refresh token");

                user = await authService.GetForRefreshAsync(userId, cancellationToken);
            }
            else
            {
                return BadRequest(new OpenIddictResponse
                {
                    Error = OpenIddictConstants.Errors.UnsupportedGrantType
                });
            }

            return SignIn(BuildPrincipal(user), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }
        catch (UnauthorizedException e)
        {
            var properties = new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = e.Message
            });
            return Forbid(properties, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }
    }

    private static ClaimsPrincipal BuildPrincipal(User user)
    {
        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, user.Id.ToString()));
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Email, user.Email));

        foreach (var code in user.Roles.SelectMany(r => r.Permissions).Select(p => p.Code).Distinct())
            identity.AddClaim(new Claim(AuthConstants.PermissionClaimType, code));

        foreach (var claim in identity.Claims)
            claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);

        return new ClaimsPrincipal(identity);
    }
}