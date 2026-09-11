using System.Security.Claims;
using Administration.Application.Auth;
using Administration.Application.Users;
using Administration.Domain.Entities;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Platform.Shared.Kernel.Services;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Administration.Api.Controllers;

[ApiController]
[Route("account")]
public class AccountController(
    UserManager<User> users,
    SignInManager<User> signIn,
    IEmailSender<User> emailSender,
    IFileStorage fileStorage,
    AdminDbContext db,
    ILogger<AccountController> logger) : Controller
{
    /// <summary>Confirms a user's email using the token from the confirmation link.</summary>
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmailAsync([FromQuery] Guid userId, [FromQuery] string token)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            logger.LogWarning("Email confirmation failed: user {UserId} not found", userId);
            return NotFound();
        }

        var result = await users.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            logger.LogWarning("Email confirmation failed for user {UserId}: {Errors}", user.Id, result.Errors);
            return BadRequest(result.Errors);
        }

        logger.LogInformation("Confirmed email for user {UserId}", user.Id);
        return Ok();
    }

    /// <summary>Resends the email confirmation link for the given email, if it belongs to a user.</summary>
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
        logger.LogInformation("Resent confirmation email to user {UserId}", user.Id);
        return Ok();
    }

    /// <summary>Sends a password reset link for the given email, if it belongs to a user.</summary>
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
        logger.LogInformation("Sent password reset email to user {UserId}", user.Id);
        return Ok();
    }

    // Password reset is now handled entirely by Pages/Account/ResetPassword.cshtml (same
    // /account/reset-password URL the email link already points to) - no separate JSON action
    // needed, and one would collide with the page's own POST handler on that route.

    /// <summary>Changes the caller's own password.</summary>
    [Authorize(Policy = "AccountMutation")]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            logger.LogWarning("Password change failed for user {UserId}: {Errors}", user.Id, result.Errors);
            return BadRequest(result.Errors);
        }

        logger.LogInformation("Changed password for user {UserId}", user.Id);
        return Ok();
    }

    /// <summary>Deletes the caller's own account.</summary>
    [Authorize(Policy = "AccountMutation")]
    [HttpDelete]
    public async Task<IActionResult> DeleteAccountAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();

        var result = await users.DeleteAsync(user);
        if (!result.Succeeded)
        {
            logger.LogWarning("Account deletion failed for user {UserId}: {Errors}", user.Id, result.Errors);
            return BadRequest(result.Errors);
        }

        await signIn.SignOutAsync();
        logger.LogInformation("Deleted account for user {UserId}", user.Id);
        return Ok();
    }

    /// <summary>Starts an external login (Google/Apple) by challenging the given provider.</summary>
    [HttpGet("external-login/{provider}")]
    public IActionResult ExternalLogin(string provider, [FromQuery] string? returnUrl)
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), values: new { returnUrl });
        var properties = signIn.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    /// <summary>Handles the external provider's callback: signs in an existing user, or links/creates one by verified email.</summary>
    [HttpGet("external-login-callback")]
    public async Task<IActionResult> ExternalLoginCallback([FromQuery] string? returnUrl)
    {
        var info = await signIn.GetExternalLoginInfoAsync();
        if (info is null)
        {
            logger.LogWarning("External login callback failed: no external login info");
            return Unauthorized();
        }

        var result = await signIn.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);

        if (result.Succeeded)
        {
            logger.LogInformation("Signed in via external login provider {Provider}", info.LoginProvider);
            return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/dashboard" : returnUrl);
        }

        if (result.IsLockedOut || result.IsNotAllowed)
        {
            logger.LogWarning("External login denied for provider {Provider}: account locked out or not allowed", info.LoginProvider);
            return Forbid();
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (email is null)
        {
            logger.LogWarning("External login failed: provider {Provider} did not return an email", info.LoginProvider);
            return BadRequest("External provider did not return an email");
        }

        if (info.Principal.FindFirstValue("email_verified") is "false")
        {
            logger.LogWarning("External login failed: provider {Provider} did not verify the email", info.LoginProvider);
            return BadRequest("External provider did not verify this email");
        }

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
            {
                logger.LogWarning("External login failed: could not create user for provider {Provider}: {Errors}",
                    info.LoginProvider, createResult.Errors);
                return BadRequest(createResult.Errors);
            }
        }

        var addLoginResult = await users.AddLoginAsync(user, info);
        if (!addLoginResult.Succeeded)
        {
            logger.LogWarning("External login failed: could not link provider {Provider} to user {UserId}: {Errors}",
                info.LoginProvider, user.Id, addLoginResult.Errors);
            return BadRequest(addLoginResult.Errors);
        }

        await signIn.SignInAsync(user, isPersistent: true);
        logger.LogInformation("Created and signed in user {UserId} via external login provider {Provider}", user.Id, info.LoginProvider);
        return LocalRedirect(returnUrl ?? "/dashboard");
    }

    /// <summary>Lists the external login providers linked to the caller's account.</summary>
    [Authorize(Policy = "AccountMutation")]
    [HttpGet("external-logins")]
    public async Task<IActionResult> GetExternalLoginsAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();

        var logins = await users.GetLoginsAsync(user);
        return Ok(logins.Select(l => l.LoginProvider));
    }

    /// <summary>Gets the caller's own profile, including their role(s) in the current project if the token is project-scoped.</summary>
    [Authorize(Policy = "AccountMutation")]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfileAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();

        var roles = await GetCurrentProjectRolesAsync(user.Id);
        return Ok(new UserProfileResponse(user.Id, user.UserName, user.FirstName, user.LastName, user.Email, user.City, user.PostCode, roles));
    }

    /// <summary>Updates the caller's own profile fields.</summary>
    [Authorize(Policy = "AccountMutation")]
    [HttpPatch("profile")]
    public async Task<IActionResult> UpdateProfileAsync([FromBody] UpdateProfileRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();

        if (request.UserName != user.UserName)
        {
            var userNameResult = await users.SetUserNameAsync(user, request.UserName);
            if (!userNameResult.Succeeded)
            {
                logger.LogWarning("Profile update failed for user {UserId}: could not set username: {Errors}",
                    user.Id, userNameResult.Errors);
                return BadRequest(userNameResult.Errors);
            }
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.City = request.City;
        user.PostCode = request.PostCode;

        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            logger.LogWarning("Profile update failed for user {UserId}: {Errors}", user.Id, result.Errors);
            return BadRequest(result.Errors);
        }

        logger.LogInformation("Updated profile for user {UserId}", user.Id);
        var roles = await GetCurrentProjectRolesAsync(user.Id);
        return Ok(new UserProfileResponse(user.Id, user.UserName, user.FirstName, user.LastName, user.Email, user.City, user.PostCode, roles));
    }

    /// <summary>Uploads or replaces the caller's own profile photo.</summary>
    [Authorize(Policy = "AccountMutation")]
    [HttpPost("profile-photo")]
    [RequestSizeLimit(130023424)]
    public async Task<IActionResult> UploadProfilePhotoAsync([FromBody] UserProfilePhotoModel request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();

        var path = await fileStorage.SaveAsync($"users/u_{user.Id}.{request.Format}",
            Convert.FromBase64String(request.PhotoBase64));

        user.ProfilePhotoPath = path;
        user.ProfilePhotoFormat = request.Format;

        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            logger.LogWarning("Profile photo upload failed for user {UserId}: {Errors}", user.Id, result.Errors);
            return BadRequest(result.Errors);
        }

        logger.LogInformation("Updated profile photo for user {UserId}", user.Id);
        return Ok();
    }

    /// <summary>Signs the caller out of the cookie session.</summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync()
    {
        var userId = User.FindFirstValue(Claims.Subject) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        await signIn.SignOutAsync();
        logger.LogInformation("Signed out user {UserId}", userId);
        return Ok();
    }

    // UserManager.GetUserAsync(User) looks up ClaimTypes.NameIdentifier, which is what cookie
    // sign-in sets - but a Bearer-authenticated (mobile) principal carries the claim as OpenIddict
    // issued it, "sub" (Claims.Subject), unmapped. Check both so either caller resolves correctly.
    private Task<User?> GetCurrentUserAsync()
    {
        var userId = User.FindFirstValue(Claims.Subject) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? Task.FromResult<User?>(null) : users.FindByIdAsync(userId);
    }

    // "project role" only makes sense relative to a project - only project-scoped tokens (from
    // the token-exchange grant) carry a project_id claim; a base authorization_code token has
    // none, so this returns no roles rather than guessing which project was meant.
    private async Task<string[]> GetCurrentProjectRolesAsync(Guid userId)
    {
        var projectIdClaim = User.FindFirstValue("project_id");
        if (projectIdClaim is null || !Guid.TryParse(projectIdClaim, out var projectId))
            return [];

        return await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(db.Roles.Where(r => r.ProjectId == projectId), ur => ur.RoleId, r => r.Id, (_, r) => r.Name!)
            .ToArrayAsync();
    }
}
