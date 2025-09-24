namespace Shared.Models.Requests.Auth;

public sealed class AppleAuthRequest
{
    public string? Code { get; set; }          // NEW: for web popup flow
    public string? IdToken { get; set; }       // optional; mobile often sends this
    public string? State { get; set; }         // recommended: verify if you store it
    public string? Nonce { get; set; }         // recommended: verify if you use it
    public string? Email { get; set; }         // optional (don’t trust blindly)
    public string? UserJson { get; set; }      // optional: raw `user` JSON Apple returns first time
}