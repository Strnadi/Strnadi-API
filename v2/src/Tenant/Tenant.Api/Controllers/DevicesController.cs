using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tenant.Api.Extensions;
using Tenant.Application.Devices;

namespace Tenant.Api.Controllers;

[ApiController]
[Route("devices")]
public class DevicesController(DevicesService devicesService) : ControllerBase
{
    /// <summary>Registers a device for push notifications.</summary>
    [Authorize]
    [HttpPost("add")]
    public async Task<IActionResult> AddAsync([FromBody] AddDeviceRequest request, CancellationToken cancellationToken)
    {
        await devicesService.AddAsync(request, this.GetCallerId(), cancellationToken);
        return Ok();
    }

    /// <summary>Updates a registered device.</summary>
    [Authorize]
    [HttpPatch("update")]
    public async Task<IActionResult> UpdateAsync([FromBody] UpdateDeviceRequest request, CancellationToken cancellationToken)
    {
        await devicesService.UpdateAsync(request, this.GetCallerId(), cancellationToken);
        return Ok();
    }

    /// <summary>Unregisters a device.</summary>
    [Authorize]
    [HttpDelete("delete/{fcmToken}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] string fcmToken, CancellationToken cancellationToken)
    {
        await devicesService.DeleteAsync(fcmToken, this.GetCallerId(), this.IsAdmin(), cancellationToken);
        return Ok();
    }
}
