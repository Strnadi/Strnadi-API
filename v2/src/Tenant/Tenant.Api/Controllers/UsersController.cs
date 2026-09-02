using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tenant.Api.Extensions;
using Tenant.Application.Common;
using Tenant.Application.Users;
using Tenant.Infrastructure.Auth;

namespace Tenant.Api.Controllers;

[ApiController]
[Route("users")]
public class UsersController(UsersService usersService) : ControllerBase
{
    /// <summary>All users. Admin only.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        return Ok(await usersService.GetAllUsersAsync(cancellationToken));
    }

    /// <summary>The caller's own id, read straight off the JWT.</summary>
    [Authorize]
    [HttpGet("get-id")]
    public IActionResult GetId()
    {
        return Ok(this.GetCallerId());
    }

    /// <summary>A user's profile; the email is only included for the user themselves or an admin.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] int id, CancellationToken cancellationToken)
    {
        return Ok(await usersService.GetUserByIdAsync(id, this.GetCallerIdOrDefault(), this.IsAdmin(), cancellationToken));
    }

    /// <summary>Updates a user's profile fields.</summary>
    [Authorize]
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        return Ok(await usersService.UpdateAsync(id, request, this.GetCallerId(), this.IsAdmin(), cancellationToken));
    }

    /// <summary>Deletes a user.</summary>
    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] int id, CancellationToken cancellationToken)
    {
        await usersService.DeleteAsync(id, this.GetCallerId(), this.IsAdmin(), cancellationToken);
        return Ok();
    }

    /// <summary>Landing point for the link in the verification email; redirects to the web confirmation page.</summary>
    [HttpGet("{userId:int}/verify-email")]
    public async Task<IActionResult> VerifyEmailAsync([FromRoute] int userId,
        [FromQuery] string jwt,
        [FromServices] LinkBuilder linkBuilder,
        CancellationToken cancellationToken)
    {
        bool verified = await usersService.VerifyEmailAsync(userId, jwt, cancellationToken);
        return RedirectPermanent(linkBuilder.EmailVerificationRedirectLink(verified));
    }

    /// <summary>Sets a new password.</summary>
    [Authorize]
    [HttpPatch("{userId:int}/change-password")]
    public async Task<IActionResult> ChangePasswordAsync([FromRoute] int userId,
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        await usersService.ChangePasswordAsync(userId, request, this.GetCallerId(), cancellationToken);
        return Ok();
    }

    /// <summary>Whether a user with this id or email already exists.</summary>
    [HttpGet("exists")]
    public async Task<IActionResult> Exists([FromQuery] int? userId,
        [FromQuery] string? email,
        CancellationToken cancellationToken)
    {
        if (userId is null && email is null)
            return BadRequest("Email and userId was not provided");

        bool exists = await usersService.ExistsAsync(userId, email, cancellationToken);
        return exists ? Conflict("Exists") : Ok();
    }
}