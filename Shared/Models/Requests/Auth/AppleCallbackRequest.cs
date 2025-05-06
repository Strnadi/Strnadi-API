namespace Shared.Models.Requests.Auth;

public class AppleCallbackRequest
{
    public string Code { get; set; }

    public string State { get; set; }

    public string? User { get; set; }
}