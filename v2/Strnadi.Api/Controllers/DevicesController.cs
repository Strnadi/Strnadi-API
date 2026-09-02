using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Strnadi.Api.Extensions;
using Strnadi.Application.Devices;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("devices")]
public class DevicesController(DevicesService devicesService) : ControllerBase
{
    [Authorize]
    [HttpPost("add")]
    public async Task<IActionResult> AddAsync([FromBody] AddDeviceRequest request, CancellationToken cancellationToken)
    {
        await devicesService.AddAsync(request, this.GetCallerId(), cancellationToken);
        return Ok();
    }

    [Authorize]
    [HttpPatch("update")]
    public async Task<IActionResult> UpdateAsync([FromBody] UpdateDeviceRequest request, CancellationToken cancellationToken)
    {
        await devicesService.UpdateAsync(request, this.GetCallerId(), cancellationToken);
        return Ok();
    }

    [Authorize]
    [HttpDelete("delete/{fcmToken}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] string fcmToken, CancellationToken cancellationToken)
    {
        await devicesService.DeleteAsync(fcmToken, this.GetCallerId(), this.IsAdmin(), cancellationToken);
        return Ok();
    }
}
