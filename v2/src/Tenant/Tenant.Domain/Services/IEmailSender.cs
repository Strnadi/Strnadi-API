namespace Tenant.Domain.Services;

public interface IEmailSender
{
    Task SendEmailVerificationAsync(string email, string? nickname, string verificationLink, CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(string email, string? nickname, string resetLink, CancellationToken cancellationToken = default);
}
