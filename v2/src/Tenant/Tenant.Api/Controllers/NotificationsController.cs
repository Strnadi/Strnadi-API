using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Shared.Infrastructure.Authorization;
using Platform.Shared.Kernel.Authorization;
using Tenant.Application.Notifications;

namespace Tenant.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/utils")]
public class NotificationsController(NotificationsService notificationsService) : ControllerBase
{
    /// <summary>Pushes a custom notification to all of a user's devices. Admin only.</summary>
    [Authorize]
    [RequirePermission(Permissions.SendNotifications)]
    [HttpPost("send-notification")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SendAsync([FromBody] SendNotificationRequest request, CancellationToken cancellationToken)
    {
        await notificationsService.SendAsync(request, cancellationToken);
        return Ok();
    }
}
