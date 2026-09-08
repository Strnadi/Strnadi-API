using System.Net;
using System.Net.Mail;
using Administration.Domain.Configuration;
using Administration.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Administration.Infrastructure.Email;

public class SmtpEmailSender(ISmtpSettings smtpSettings, ILogger<SmtpEmailSender> logger) : IEmailSender<User>
{
    public Task SendConfirmationLinkAsync(User user, string email, string confirmationLink)
    {
        var namePart = string.IsNullOrEmpty(user.FirstName) ? "" : $" s přezdívkou <strong>{user.FirstName}</strong>";
        var body = $"""
                    <p style='font-size:1rem'>
                    Děkujeme za zájem o projekt občanské vědy Nářečí českých strnadů.<br>
                    <br>
                    Registraci nového uživatele{namePart} potvrdíte kliknutím na <a href='{confirmationLink}'>tento link</a>.<br>

                    Pokud jste se do projektu neregistrovali nebo jste zadali tuto e-mailovou adresu omylem, zprávu ignorujte.<br>
                    <br>
                    </p>
                    <h3>Vaši strnadi</h3><br>
                    <br>
                    <a href='https://www.strnadi.cz'>www.strnadi.cz</a>
                    """;

        return SendAsync(email, "Nářečí českých strnadů – potvrzení nového uživatele", body);
    }

    public Task SendPasswordResetLinkAsync(User user, string email, string resetLink)
    {
        var namePart = string.IsNullOrEmpty(user.FirstName) ? "" : $" s přezdívkou <strong>{user.FirstName}</strong>";
        var body = $"""
                    Nové heslo pro váš uživatelský účet v projektu Nářečí českých strnadů{namePart} můžete zvolit online po kliknutí na <a href='{resetLink}'>tento link</a>. <br>
                    Pokud heslo měnit nechcete, zprávu ignorujte. <br>
                    <br>
                    <h5>Vaši strnadi</h5><br>
                    <br>
                    <a href='https://www.strnadi.cz'>www.strnadi.cz</a>
                    """;

        return SendAsync(email, "Nářečí českých strnadů – zapomenuté heslo", body);
    }

    public Task SendPasswordResetCodeAsync(User user, string email, string resetCode)
    {
        var namePart = string.IsNullOrEmpty(user.FirstName) ? "" : $" s přezdívkou <strong>{user.FirstName}</strong>";
        var body = $"""
                    Váš kód pro obnovu hesla k účtu v projektu Nářečí českých strnadů{namePart}: <strong>{resetCode}</strong><br>
                    Pokud heslo měnit nechcete, zprávu ignorujte. <br>
                    <br>
                    <h5>Vaši strnadi</h5><br>
                    <br>
                    <a href='https://www.strnadi.cz'>www.strnadi.cz</a>
                    """;

        return SendAsync(email, "Nářečí českých strnadů – kód pro obnovu hesla", body);
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
