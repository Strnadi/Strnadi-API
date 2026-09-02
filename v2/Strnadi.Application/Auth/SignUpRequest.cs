namespace Strnadi.Application.Auth;

public record SignUpRequest(
    string Email,
    string FirstName,
    string LastName,
    string? Nickname,
    string? Password,
    int? PostCode,
    string? City,
    bool Consent,
    string? AppleId,
    string? GoogleId);
