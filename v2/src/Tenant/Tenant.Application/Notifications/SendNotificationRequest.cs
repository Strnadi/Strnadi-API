namespace Tenant.Application.Notifications;

public record SendNotificationRequest(
    int UserId,
    string? TitleEn,
    string? BodyEn,
    string? TitleDe,
    string? BodyDe,
    string? TitleCs,
    string? BodyCs);
