namespace Strnadi.Application.Auth;

public record AuthResponse(string Jwt);

public record SocialAuthResponse(bool Exists, string? Jwt, string? Email, string? FirstName, string? LastName);

public record GoogleSignUpResponse(string Email, string GoogleId, string? FirstName, string? LastName);
