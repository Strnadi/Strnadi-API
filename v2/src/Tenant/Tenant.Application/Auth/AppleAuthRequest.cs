namespace Tenant.Application.Auth;

public record AppleAuthRequest(string IdToken, string UserIdentifier, string? GivenName, string? FamilyName);
