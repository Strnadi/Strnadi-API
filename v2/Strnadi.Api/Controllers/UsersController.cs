using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Strnadi.Api.Extensions;
using Strnadi.Application.Common;
using Strnadi.Application.Users;
using Strnadi.Infrastructure.Auth;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("users")]
public class UsersController(UsersService usersService) : ControllerBase
{
    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        return Ok(await usersService.GetAllUsersAsync(cancellationToken));
    }

    [Authorize]
    [HttpGet("get-id")]
    public IActionResult GetId()
    {
        return Ok(this.GetCallerId());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] int id, CancellationToken cancellationToken)
    {
        return Ok(await usersService.GetUserByIdAsync(id, this.GetCallerIdOrDefault(), this.IsAdmin(), cancellationToken));
    }

    [Authorize]
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        return Ok(await usersService.UpdateAsync(id, request, this.GetCallerId(), this.IsAdmin(), cancellationToken));
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] int id, CancellationToken cancellationToken)
    {
        await usersService.DeleteAsync(id, this.GetCallerId(), this.IsAdmin(), cancellationToken);
        return Ok();
    }

    [HttpGet("{userId:int}/verify-email")]
    public async Task<IActionResult> VerifyEmailAsync([FromRoute] int userId,
        [FromQuery] string jwt,
        [FromServices] LinkBuilder linkBuilder,
        CancellationToken cancellationToken)
    {
        bool verified = await usersService.VerifyEmailAsync(userId, jwt, cancellationToken);
        return RedirectPermanent(linkBuilder.EmailVerificationRedirectLink(verified));
    }

    [Authorize]
    [HttpPatch("{userId:int}/change-password")]
    public async Task<IActionResult> ChangePasswordAsync([FromRoute] int userId,
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        await usersService.ChangePasswordAsync(userId, request, this.GetCallerId(), cancellationToken);
        return Ok();
    }

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