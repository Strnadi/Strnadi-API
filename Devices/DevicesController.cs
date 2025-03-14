using Auth.Services;
using Microsoft.AspNetCore.Mvc;
using Repository;
using Shared.Extensions;
using Shared.Models.Requests;
using Shared.Models.Requests.Devices;

namespace Devices;

[ApiController]
[Route("/devices")]
public class DevicesController : ControllerBase
{
    [HttpPost("add")]
    public async Task<IActionResult> Add([FromBody] AddDeviceRequest request,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo,
        [FromServices] DevicesRepository devicesRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? email)) 
            return Unauthorized();
        
        if (request.UserEmail != email)
            return BadRequest("Invalid email");

        if (!await usersRepo.ExistsAsync(email))
            return Conflict("User doesn't exist");

        bool success = await devicesRepo.AddAsync(request);
        
        return success ? Ok() : StatusCode(500, "Failed to add device");
    }

    [HttpPatch("update")]
    public async Task<IActionResult> Update([FromBody] UpdateDeviceRequest request,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo,
        [FromServices] DevicesRepository devicesRepo)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();
        
        if (!await usersRepo.ExistsAsync(email!))
            return Conflict("User doesn't exist");

        bool success = await devicesRepo.UpdateAsync(request);
        
        return success ? Ok() : StatusCode(500, "Failed to update device");
    }
}