using ApiGateway.Services;
using Microsoft.AspNetCore.Mvc;
using Models.Requests;
using Shared.Communication;
using Shared.Extensions;

namespace ApiGateway.Controllers;

[ApiController]
[Route("devices")]
public class DevicesController : ControllerBase
{
    private readonly JwtService _jwtService;
    
    public DevicesController(JwtService jwtService)
    {
        _jwtService = jwtService;
    }
    
    [HttpPost("device")]
    public async Task<IActionResult> Device([FromBody] DeviceRequest model,
        [FromServices] DagDevicesControllerClient client)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!_jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();
        
        HttpRequestResult? response = await client.Device(email!, model);

        if (response is null)
            return await this.HandleErrorResponseAsync(response);

        return Ok();
    }
}