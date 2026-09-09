namespace Tenant.Application.Notifications;

public record SendNotificationRequest(
    Guid UserId,
    string? TitleEn,
    string? BodyEn,
    string? TitleDe,
    string? BodyDe,
    string? TitleCs,
    string? BodyCs);
