namespace Shared.Models.Requests.Auth;

public class AppleAuthRequest
{
    public string? IdToken { get; set; }
    public string? email { get; set; }
    public string? givenName { get; set; }
    public string? familyName { get; set; }
    public string? userIdentifier { get; set; }
}