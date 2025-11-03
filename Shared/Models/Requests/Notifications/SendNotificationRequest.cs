namespace Shared.Models.Requests.Notifications;

public class SendNotificationRequest
{
    public int UserId { get; set; }
    
    public string? TitleEn { get; set; }
    
    public string? BodyEn { get; set; }
    
    public string? TitleDe { get; set; }
    
    public string? BodyDe { get; set; }
    
    public string? TitleCz { get; set; }
    
    public string? BodyCz { get; set; }
}