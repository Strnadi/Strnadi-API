using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Strnadi.Application.Notifications;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("utils")]
public class NotificationsController(NotificationsService notificationsService) : ControllerBase
{
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("send-notification")]
    public async Task<IActionResult> SendAsync([FromBody] SendNotificationRequest request, CancellationToken cancellationToken)
    {
        await notificationsService.SendAsync(request, cancellationToken);
        return Ok();
    }
}
