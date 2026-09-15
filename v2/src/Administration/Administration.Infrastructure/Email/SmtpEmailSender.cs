using System.Net;
using System.Net.Mail;
using Administration.Domain.Configuration;
using Administration.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Administration.Infrastructure.Email;

public class SmtpEmailSender(ISmtpSettings smtpSettings, ILogger<SmtpEmailSender> logger) : IEmailSender<User>
{
    private const string ColorBg = "#FAF8F3";
    private const string ColorSurfaceAlt = "#F1EDE3";
    private const string ColorBorder = "#E3DDCB";
    private const string ColorText = "#211F1A";
    private const string ColorTextMuted = "#6B6558";
    private const string ColorPrimary = "#1F4D3D";
    private const string FontFamily = "'Inter', system-ui, -apple-system, 'Segoe UI', sans-serif";

    public Task SendConfirmationLinkAsync(User user, string email, string confirmationLink)
    {
        var namePart = GetNamePart(user);

        var (subject, body) = user.PreferredLanguage switch
        {
            "en" => (
                "Yellowhammer Dialects – confirm your new account",
                P($"Thank you for your interest in the <strong>Yellowhammer Dialects</strong> citizen science project.") +
                P($"Please confirm your registration{namePart} by clicking the button below.") +
                Button(confirmationLink, "Confirm registration") +
                Muted("If you did not register for this project or entered this email address by mistake, please ignore this message.") +
                Signature("Your Strnadi team")),
            "de" => (
                "Dialekte des Goldammers – Bestätigung des neuen Benutzers",
                P("Vielen Dank für Ihr Interesse am Citizen-Science-Projekt <strong>Dialekte des Goldammers</strong>.") +
                P($"Bitte bestätigen Sie Ihre Registrierung{namePart}, indem Sie auf die Schaltfläche unten klicken.") +
                Button(confirmationLink, "Registrierung bestätigen") +
                Muted("Wenn Sie sich nicht für dieses Projekt registriert oder diese E-Mail-Adresse versehentlich angegeben haben, ignorieren Sie diese Nachricht.") +
                Signature("Ihr Strnadi-Team")),
            _ => (
                "Nářečí českých strnadů – potvrzení nového uživatele",
                P("Děkujeme za zájem o projekt občanské vědy <strong>Nářečí českých strnadů</strong>.") +
                P($"Registraci nového uživatele{namePart} potvrdíte kliknutím na tlačítko níže.") +
                Button(confirmationLink, "Potvrdit registraci") +
                Muted("Pokud jste se do projektu neregistrovali nebo jste zadali tuto e-mailovou adresu omylem, zprávu ignorujte.") +
                Signature("Vaši strnadi"))
        };

        return SendAsync(email, subject, Wrap(body));
    }

    public Task SendPasswordResetLinkAsync(User user, string email, string resetLink)
    {
        var namePart = GetNamePart(user);

        var (subject, body) = user.PreferredLanguage switch
        {
            "en" => (
                "Yellowhammer Dialects – forgotten password",
                P($"You can set a new password for your account in the <strong>Yellowhammer Dialects</strong> project{namePart} by clicking the button below.") +
                Button(resetLink, "Set new password") +
                Muted("If you do not want to change your password, please ignore this message.") +
                Signature("Your Strnadi team")),
            "de" => (
                "Dialekte des Goldammers – Passwort vergessen",
                P($"Ein neues Passwort für Ihr Benutzerkonto im Projekt <strong>Dialekte des Goldammers</strong>{namePart} können Sie festlegen, indem Sie auf die Schaltfläche unten klicken.") +
                Button(resetLink, "Neues Passwort festlegen") +
                Muted("Wenn Sie Ihr Passwort nicht ändern möchten, ignorieren Sie diese Nachricht.") +
                Signature("Ihr Strnadi-Team")),
            _ => (
                "Nářečí českých strnadů – zapomenuté heslo",
                P($"Nové heslo pro váš uživatelský účet v projektu <strong>Nářečí českých strnadů</strong>{namePart} můžete zvolit po kliknutí na tlačítko níže.") +
                Button(resetLink, "Nastavit nové heslo") +
                Muted("Pokud heslo měnit nechcete, zprávu ignorujte.") +
                Signature("Vaši strnadi"))
        };

        return SendAsync(email, subject, Wrap(body));
    }

    public Task SendPasswordResetCodeAsync(User user, string email, string resetCode)
    {
        var namePart = GetNamePart(user);

        var (subject, body) = user.PreferredLanguage switch
        {
            "en" => (
                "Yellowhammer Dialects – password recovery code",
                P($"Your password recovery code for your account in the <strong>Yellowhammer Dialects</strong> project{namePart}:") +
                CodeBlock(resetCode) +
                Muted("If you do not want to change your password, please ignore this message.") +
                Signature("Your Strnadi team")),
            "de" => (
                "Dialekte des Goldammers – Code zur Passwortwiederherstellung",
                P($"Ihr Code zur Wiederherstellung des Passworts für Ihr Konto im Projekt <strong>Dialekte des Goldammers</strong>{namePart}:") +
                CodeBlock(resetCode) +
                Muted("Wenn Sie Ihr Passwort nicht ändern möchten, ignorieren Sie diese Nachricht.") +
                Signature("Ihr Strnadi-Team")),
            _ => (
                "Nářečí českých strnadů – kód pro obnovu hesla",
                P($"Váš kód pro obnovu hesla k účtu v projektu <strong>Nářečí českých strnadů</strong>{namePart}:") +
                CodeBlock(resetCode) +
                Muted("Pokud heslo měnit nechcete, zprávu ignorujte.") +
                Signature("Vaši strnadi"))
        };

        return SendAsync(email, subject, Wrap(body));
    }

    private static string GetNamePart(User user)
    {
        if (string.IsNullOrEmpty(user.FirstName))
            return "";

        return user.PreferredLanguage switch
        {
            "en" => $" (nickname <strong>{user.FirstName}</strong>)",
            "de" => $" mit dem Spitznamen <strong>{user.FirstName}</strong>",
            _ => $" s přezdívkou <strong>{user.FirstName}</strong>"
        };
    }

    /// <summary>Wraps message-specific content in the branded card layout shared by all emails.</summary>
    private static string Wrap(string bodyHtml) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:{ColorBg};padding:48px 16px;font-family:{FontFamily};">
          <tr>
            <td align="center">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;background-color:#FFFFFF;border:1px solid {ColorBorder};border-radius:14px;overflow:hidden;">
                <tr>
                  <td style="background-color:{ColorPrimary};padding:24px 32px;">
                    <span style="color:#FFFFFF;font-size:22px;font-weight:600;letter-spacing:0.02em;font-family:{FontFamily};">Strnadi</span>
                  </td>
                </tr>
                <tr>
                  <td style="padding:32px;color:{ColorText};font-size:16px;line-height:1.5;font-family:{FontFamily};">
                    {bodyHtml}
                  </td>
                </tr>
                <tr>
                  <td style="padding:16px 32px 32px;border-top:1px solid {ColorBorder};">
                    <a href="https://www.strnadi.cz" style="color:{ColorPrimary};font-size:12px;text-decoration:none;font-family:{FontFamily};">www.strnadi.cz</a>
                  </td>
                </tr>
              </table>
            </td>
          </tr>
        </table>
        """;

    private static string P(string html) =>
        $"""<p style="margin:0 0 16px;">{html}</p>""";

    private static string Muted(string html) =>
        $"""<p style="margin:0 0 24px;color:{ColorTextMuted};font-size:14px;line-height:1.5;">{html}</p>""";

    private static string Signature(string html) =>
        $"""<p style="margin:0;">{html}</p>""";

    private static string Button(string href, string label) => $"""
        <p style="margin:8px 0 24px;">
          <a href="{href}" style="display:inline-block;background-color:{ColorPrimary};color:#FFFFFF;text-decoration:none;font-weight:500;font-size:15px;padding:10px 20px;border-radius:8px;font-family:{FontFamily};">{label}</a>
        </p>
        """;

    private static string CodeBlock(string code) => $"""
        <div style="background-color:{ColorSurfaceAlt};border:1px solid {ColorBorder};border-radius:8px;padding:16px;text-align:center;font-size:28px;font-weight:600;letter-spacing:0.2em;color:{ColorPrimary};margin:8px 0 24px;font-family:{FontFamily};">{code}</div>
        """;

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
