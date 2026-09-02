namespace Strnadi.Domain.Services;

public record AppleIdTokenPayload(string Subject, string? Email);

public interface IAppleIdTokenValidator
{
    Task<AppleIdTokenPayload?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
