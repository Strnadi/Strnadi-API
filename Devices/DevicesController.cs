using Auth.Services;
using Microsoft.AspNetCore.Mvc;
using Repository;
using Shared.Extensions;
using Shared.Models.Requests;

namespace Devices;

[ApiController]
[Route("/devices")]
public class DevicesController : ControllerBase
{
    [HttpPost("add")]
    public async Task<IActionResult> Add([FromBody] AddDeviceModel model,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo,
        [FromServices] DevicesRepository devicesRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? email)) 
            return Unauthorized();
        
        if (model.UserEmail != email)
            return BadRequest("Invalid email");

        if (!await usersRepo.ExistsAsync(email))
            return Conflict("User doesn't exist");

        bool success = await devicesRepo.AddAsync(model);
        
        return success ? Ok() : StatusCode(500, "Failed to add device");
    }
}