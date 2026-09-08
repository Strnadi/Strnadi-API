using System.Security.Claims;
using Administration.Application.Auth;
using Administration.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Administration.Api.Controllers;

[ApiController]
[Route("account")]
public class AccountController(UserManager<User> users, SignInManager<User> signIn, IEmailSender<User> emailSender) : Controller
{
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmailAsync([FromQuery] Guid userId, [FromQuery] string token)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
            return NotFound();

        var result = await users.ConfirmEmailAsync(user, token);
        return result.Succeeded ? Ok() : BadRequest(result.Errors);
    }

    [HttpPost("resend-confirmation-email")]
    public async Task<IActionResult> ResendConfirmationEmailAsync([FromBody] ResendConfirmationRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null)
            return Ok();

        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var confirmLink = $"{Request.Scheme}://{Request.Host}/account/confirm-email" +
            $"?userId={user.Id}&token={Uri.EscapeDataString(token)}";

        await emailSender.SendConfirmationLinkAsync(user, user.Email!, confirmLink);
        return Ok();
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPasswordAsync([FromBody] ForgotPasswordRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null)
            return Ok();

        var token = await users.GeneratePasswordResetTokenAsync(user);
        var resetLink = $"{Request.Scheme}://{Request.Host}/account/reset-password" +
            $"?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";

        await emailSender.SendPasswordResetLinkAsync(user, user.Email!, resetLink);
        return Ok();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPasswordAsync([FromBody] ResetPasswordRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null)
            return BadRequest("Invalid token");

        var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        return result.Succeeded ? Ok() : BadRequest("Invalid token");
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordRequest request)
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        return result.Succeeded ? Ok() : BadRequest(result.Errors);
    }

    [Authorize]
    [HttpDelete]
    public async Task<IActionResult> DeleteAccountAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var result = await users.DeleteAsync(user);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        await signIn.SignOutAsync();
        return Ok();
    }
    
    [HttpGet("external-login/{provider}")]
    public IActionResult ExternalLogin(string provider, [FromQuery] string? returnUrl)
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), values: new { returnUrl });
        var properties = signIn.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet("external-login-callback")]
    public async Task<IActionResult> ExternalLoginCallback([FromQuery] string? returnUrl)
    {
        var info = await signIn.GetExternalLoginInfoAsync();
        if (info is null)
            return Unauthorized();

        var result = await signIn.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);

        if (result.Succeeded)
            return LocalRedirect(returnUrl ?? "/");

        if (result.IsLockedOut || result.IsNotAllowed)
            return Forbid();

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (email is null)
            return BadRequest("External provider did not return an email");

        if (info.Principal.FindFirstValue("email_verified") is "false")
            return BadRequest("External provider did not verify this email");

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new User
            {
                UserName = null,
                Email = email,
                FirstName = info.Principal!.FindFirstValue(ClaimTypes.GivenName) ?? string.Empty,
                LastName = info.Principal!.FindFirstValue(ClaimTypes.Surname) ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                EmailConfirmed = true
            };

            var createResult = await users.CreateAsync(user);
            if (!createResult.Succeeded)
                return BadRequest(createResult.Errors);
        }

        var addLoginResult = await users.AddLoginAsync(user, info);
        if (!addLoginResult.Succeeded)
            return BadRequest(addLoginResult.Errors);

        await signIn.SignInAsync(user, isPersistent: true);
        return LocalRedirect(returnUrl ?? "/");
    }

    [Authorize]
    [HttpGet("external-logins")]
    public async Task<IActionResult> GetExternalLoginsAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var logins = await users.GetLoginsAsync(user);
        return Ok(logins.Select(l => l.LoginProvider));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync()
    {
        await signIn.SignOutAsync();
        return Ok();
    }
}
