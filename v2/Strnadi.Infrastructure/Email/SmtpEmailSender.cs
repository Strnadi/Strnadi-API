using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Strnadi.Domain.Configuration;
using Strnadi.Domain.Services;

namespace Strnadi.Infrastructure.Email;

public class SmtpEmailSender(ISmtpSettings smtpSettings, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public Task SendEmailVerificationAsync(string email, string? nickname, string verificationLink, CancellationToken cancellationToken = default)
    {
        var greeting = string.IsNullOrWhiteSpace(nickname) ? "Ahoj" : $"Ahoj {nickname}";
        var body = $"""
                    <p>{greeting},</p>
                    <p>Pro dokončení registrace potvrďte prosím svůj e-mail kliknutím na odkaz níže.</p>
                    <p><a href="{verificationLink}">Ověřit e-mail</a></p>
                    """;

        return SendAsync(email, "Ověření e-mailu — Strnadi", body);
    }

    public Task SendPasswordResetAsync(string email, string? nickname, string resetLink, CancellationToken cancellationToken = default)
    {
        var greeting = string.IsNullOrWhiteSpace(nickname) ? "Ahoj" : $"Ahoj {nickname}";
        var body = $"""
                    <p>{greeting},</p>
                    <p>Pro obnovení hesla klikněte na odkaz níže.</p>
                    <p><a href="{resetLink}">Obnovit heslo</a></p>
                    <p>Pokud jste o obnovení hesla nežádali, tento e-mail ignorujte.</p>
                    """;

        return SendAsync(email, "Obnova hesla — Strnadi", body);
    }

    private async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        using var message = new MailMessage(smtpSettings.Email, toEmail, subject, htmlBody) { IsBodyHtml = true };
        using var client = new SmtpClient(smtpSettings.Domain, smtpSettings.Port)
        {
            EnableSsl = smtpSettings.EnableSsl,
            Credentials = new NetworkCredential(smtpSettings.Username, smtpSettings.Password)
        };

        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to send email to {Email}", toEmail);
        }
    }
}
