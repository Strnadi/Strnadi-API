namespace Shared.Models.Requests.Auth;

public class AppleAuthRequest
{
    public string? IdToken { get; set; }
    public string? Email { get; set; }
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? UserIdentifier { get; set; }
}