using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Tenant.Domain.Configuration;
using Tenant.Domain.Services;

namespace Tenant.Infrastructure.Email;

public class SmtpEmailSender(ISmtpSettings smtpSettings, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public Task SendEmailVerificationAsync(string email, string? nickname, string verificationLink, CancellationToken cancellationToken = default)
    {
        var nicknamePart = string.IsNullOrEmpty(nickname) ? "" : $" s přezdívkou <strong>{nickname}</strong>";
        var body = $"""
                    <p style='font-size:1rem'>
                    Děkujeme za zájem o projekt občanské vědy Nářečí českých strnadů.<br>
                    <br>
                    Registraci nového uživatele{nicknamePart} potvrdíte kliknutím na <a href='{verificationLink}'>tento link</a>.<br>

                    Pokud jste se do projektu neregistrovali nebo jste zadali tuto e-mailovou adresu omylem, zprávu ignorujte.<br>
                    <br>
                    </p>
                    <h3>Vaši strnadi</h3><br>
                    <br>
                    <a href='https://www.strnadi.cz'>www.strnadi.cz</a>
                    """;

        return SendAsync(email, "Nářečí českých strnadů – potvrzení nového uživatele", body);
    }

    public Task SendPasswordResetAsync(string email, string? nickname, string resetLink, CancellationToken cancellationToken = default)
    {
        var nicknamePart = string.IsNullOrEmpty(nickname) ? "" : $" s přezdívkou <strong>{nickname}</strong>";
        var body = $"""
                    Nové heslo pro váš uživatelský účet v projektu Nářečí českých strnadů{nicknamePart} můžete zvolit online po kliknutí na <a href='{resetLink}'>tento link</a>. <br>
                    Pokud heslo měnit nechcete, zprávu ignorujte. <br>
                    <br>
                    <h5>Vaši strnadi</h5><br>
                    <br>
                    <a href='https://www.strnadi.cz'>www.strnadi.cz</a>
                    """;

        return SendAsync(email, "Nářečí českých strnadů – zapomenuté heslo", body);
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
            logger.LogError(e, "Failed to send email");
        }
    }
}
