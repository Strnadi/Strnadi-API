using Auth.Services;
using Email;
using Repository;
using Microsoft.AspNetCore.Mvc;
using Models.Requests;
using Shared.Extensions;

namespace Users;

[ApiController]
[Route("users")]
public class UsersController : ControllerBase
{
    [HttpGet("{email}")]
    public async Task<IActionResult> Get(string email,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null) 
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? emailFromJwt))
            return Unauthorized();
        
        if (email != emailFromJwt)
            return Unauthorized("Invalid email");
        
        if (!await usersRepo.ExistsAsync(email))
            return NotFound("User not found");

        if (!await usersRepo.IsAdminAsync(email))
            return Unauthorized("User is not an admin");
        
        var user = await usersRepo.GetUserByEmail(email);
        
        if (user is null)
            return StatusCode(500, "Failed to get user");

        return Ok(user);
    }

    [HttpPatch("{email}/verify-email")]
    public async Task<IActionResult> VerifyEmailAsync(string email,
        [FromServices] JwtService jwtService,
        [FromServices] LinkGenerator linkGenerator,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null) 
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? emailFromJwt))
            return Unauthorized();

        if (email != emailFromJwt)
            return Unauthorized("Invalid email");
            
        if (!await usersRepo.ExistsAsync(email))
            return NotFound("User not found");
        
        bool verified = await usersRepo.VerifyEmailAsync(email);
        
        string redirectionPage = linkGenerator.GenerateEmailVerificationRedirectionLink(verified);
        return RedirectPermanent(redirectionPage);
    }

    [HttpPatch("{email}/change-password")]
    public async Task<IActionResult> ChangePasswordAsync(string email,
        [FromBody] ChangePasswordRequest request,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null) 
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? emailFromJwt))
            return Unauthorized();

        if (email != emailFromJwt)
            return Unauthorized("Invalid email");
            
        if (!await usersRepo.ExistsAsync(email))
            return NotFound("User not found");

        bool changed = await usersRepo.ChangePasswordAsync(email, request.NewPassword);
        
        if (!changed)
            return StatusCode(500, "Failed to change password");

        return Ok();
    }
}