namespace Strnadi.Domain.Services;

public interface IPushNotificationService
{
    Task SendVisibleNotificationAsync(string fcmToken, string title, string body, CancellationToken cancellationToken = default);

    Task SendInvisibleNotificationAsync(string fcmToken, IReadOnlyDictionary<string, string?> data, CancellationToken cancellationToken = default);
}
