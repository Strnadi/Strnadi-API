using Microsoft.Extensions.Logging;
using Tenant.Domain.Persistence.Repositories;
using Tenant.Domain.Services;

namespace Tenant.Application.Notifications;

public class NotificationsService(
    IDevicesRepository devices,
    IPushNotificationService pushNotificationService,
    ILogger<NotificationsService> logger)
{
    public async Task SendAsync(SendNotificationRequest request, CancellationToken cancellationToken = default)
    {
        var userDevices = await devices.GetAllByUserIdAsync(request.UserId, cancellationToken);

        var data = new Dictionary<string, string?>
        {
            { "action", "custom" },
            { "titleEn", request.TitleEn },
            { "bodyEn", request.BodyEn },
            { "titleDe", request.TitleDe },
            { "bodyDe", request.BodyDe },
            { "titleCs", request.TitleCs },
            { "bodyCs", request.BodyCs },
        };

        foreach (var device in userDevices)
        {
            try
            {
                await pushNotificationService.SendInvisibleNotificationAsync(device.FcmToken, data, cancellationToken);
            }
            catch (Exception ex)
            {
                // Best-effort broadcast: one stale/invalid device token shouldn't fail the whole send.
                logger.LogError(ex, "Failed to send notification to device {FcmToken}", device.FcmToken);
            }
        }
    }
}
