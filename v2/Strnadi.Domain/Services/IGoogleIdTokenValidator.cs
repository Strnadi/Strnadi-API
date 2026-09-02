namespace Strnadi.Domain.Services;

public record GoogleIdTokenPayload(string Subject, string Email, string? GivenName, string? FamilyName);

public interface IGoogleIdTokenValidator
{
    Task<GoogleIdTokenPayload?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
