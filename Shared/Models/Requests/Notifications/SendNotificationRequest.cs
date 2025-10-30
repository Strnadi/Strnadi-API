namespace Shared.Models.Requests.Notifications;

public class SendNotificationRequest
{
    public int UserId { get; set; }
    
    public string Title { get; set; }
    
    public string Body { get; set; }
}