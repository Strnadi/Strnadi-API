using Administration.Api.Extensions;
using Administration.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Administration.Api.Controllers;

// Account mutations only — see TokenController for how a client actually obtains a token.
[ApiController]
[Route("auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    public record SignUpRequest(string Email, string Password);
    public record IdTokenRequest(string IdToken);
    public record AppleIdentifierRequest(string UserIdentifier);

    /// <summary>Creates an account with email + password.</summary>
    [HttpPost("sign-up")]
    public async Task<IActionResult> SignUpAsync([FromBody] SignUpRequest request, CancellationToken cancellationToken)
    {
        var user = await authService.SignUpAsync(request.Email, request.Password, cancellationToken);
        return Ok(new { user.Id, user.Email });
    }

    /// <summary>Validates a Google ID token for a brand-new account; the account itself is created by a later sign-up call.</summary>
    [HttpPost("sign-up-google")]
    public async Task<IActionResult> SignUpGoogleAsync([FromBody] IdTokenRequest request, CancellationToken cancellationToken)
    {
        return Ok(await authService.SignUpGoogleAsync(request.IdToken, cancellationToken));
    }

    /// <summary>Same as <see cref="SignUpGoogleAsync"/>, for Sign in with Apple.</summary>
    [HttpPost("sign-up-apple")]
    public async Task<IActionResult> SignUpAppleAsync([FromBody] IdTokenRequest request, CancellationToken cancellationToken)
    {
        return Ok(await authService.SignUpAppleAsync(request.IdToken, cancellationToken));
    }

    /// <summary>Links a Google account to the caller's existing account.</summary>
    [Authorize]
    [HttpPost("link-google")]
    public async Task<IActionResult> LinkGoogleAsync([FromBody] IdTokenRequest request, CancellationToken cancellationToken)
    {
        await authService.LinkGoogleAsync(this.GetCallerId(), request.IdToken, cancellationToken);
        return Ok();
    }

    /// <summary>Links an Apple account to the caller's existing account.</summary>
    [Authorize]
    [HttpPost("link-apple")]
    public async Task<IActionResult> LinkAppleAsync([FromBody] AppleIdentifierRequest request, CancellationToken cancellationToken)
    {
        await authService.LinkAppleAsync(this.GetCallerId(), request.UserIdentifier, cancellationToken);
        return Ok();
    }
}