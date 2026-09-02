using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Strnadi.Api.Extensions;
using Strnadi.Application.Auth;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    [Authorize]
    [HttpGet("verify-jwt")]
    public async Task<IActionResult> VerifyJwtAsync(CancellationToken cancellationToken)
    {
        bool verified = await authService.IsEmailVerifiedAsync(this.GetCallerId(), cancellationToken);
        return verified ? Ok() : StatusCode(StatusCodes.Status403Forbidden);
    }

    [HttpGet("renew-jwt")]
    public async Task<IActionResult> RenewJwtAsync(CancellationToken cancellationToken)
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return BadRequest("No JWT provided");

        var token = header["Bearer ".Length..];
        return Ok(await authService.RenewTokenAsync(token, cancellationToken));
    }

    [HttpPost("sign-up-google")]
    public async Task<IActionResult> SignUpGoogleAsync([FromBody] GoogleAuthRequest request, CancellationToken cancellationToken)
    {
        return Ok(await authService.SignUpGoogleAsync(request, cancellationToken));
    }

    [HttpPost("login-google")]
    public async Task<IActionResult> LoginGoogleAsync([FromBody] GoogleAuthRequest request, CancellationToken cancellationToken)
    {
        return Ok(await authService.LoginGoogleAsync(request, cancellationToken));
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleAsync([FromBody] GoogleAuthRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.GoogleAsync(request, this.GetCallerIdOrDefault(), cancellationToken);
        return result is null ? Ok() : Ok(result);
    }

    [HttpPost("apple")]
    public async Task<IActionResult> AppleAsync([FromBody] AppleAuthRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.AppleAsync(request, this.GetCallerIdOrDefault(), cancellationToken);
        return result is null ? Ok() : Ok(result);
    }

    [HttpPost("apple-callback")]
    public IActionResult AppleCallback(
        [FromForm(Name = "user")] string? user,
        [FromForm(Name = "state")] string? state,
        [FromForm(Name = "id_token")] string? idToken)
    {
        if (state is null)
            return BadRequest();

        var returnUrl = state.Split('|')[0];
        return Redirect($"{returnUrl}#user={user}&id_token={idToken}");
    }

    [HttpPost("apple/callback")]
    public IActionResult AppleCallbackMobile(
        [FromServices] IConfiguration configuration,
        [FromForm(Name = "user")] string? user,
        [FromForm(Name = "state")] string? state,
        [FromForm(Name = "id_token")] string? idToken,
        [FromForm(Name = "code")] string? code)
    {
        var androidPackage = configuration["Auth:Android:Package"] ?? "com.delta.strnadi";

        var query = HttpUtility.ParseQueryString(string.Empty);
        if (code is not null) query["code"] = code;
        if (state is not null) query["state"] = state;
        if (idToken is not null) query["id_token"] = idToken;
        if (user is not null) query["user"] = user;

        return Redirect($"intent://callback?{query}#Intent;scheme=signinwithapple;package={androidPackage};end");
    }

    [HttpGet("has-apple-id")]
    public async Task<IActionResult> HasAppleIdAsync([FromQuery] int userId, CancellationToken cancellationToken)
    {
        return await authService.HasAppleIdAsync(userId, cancellationToken) ? Ok() : Conflict();
    }

    [HttpGet("has-google-id")]
    public async Task<IActionResult> HasGoogleIdAsync([FromQuery] int userId, CancellationToken cancellationToken)
    {
        return await authService.HasGoogleIdAsync(userId, cancellationToken) ? Ok() : Conflict();
    }

    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        return Ok(await authService.LoginAsync(request, cancellationToken));
    }

    [HttpPost("sign-up")]
    public async Task<IActionResult> SignUpAsync([FromBody] SignUpRequest request, CancellationToken cancellationToken)
    {
        return Ok(await authService.SignUpAsync(request, cancellationToken));
    }

    [Authorize]
    [HttpGet("{userId:int}/resend-verify-email")]
    public async Task<IActionResult> ResendVerifyEmailAsync([FromRoute] int userId, CancellationToken cancellationToken)
    {
        await authService.ResendVerificationEmailAsync(userId, this.GetCallerId(), cancellationToken);
        return Ok();
    }

    [HttpGet("{email}/reset-password")]
    public async Task<IActionResult> ResetPasswordAsync([FromRoute] string email, CancellationToken cancellationToken)
    {
        await authService.RequestPasswordResetAsync(email, cancellationToken);
        return Ok();
    }
}
