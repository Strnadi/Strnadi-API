/*
 * Copyright (C) 2024 Stanislav Motsnyi
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System.Net;
using System.Net.Mail;
using System.Security;
using Microsoft.AspNetCore.Mvc;
using Shared.Logging;
using LogLevel = Shared.Logging.LogLevel;

namespace ApiGateway.Services;

public interface IEmailSender
{
    void SendMessage(string emailAddress, string subject, string body);
}

public class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    
    private string _smtpServerDomain => _configuration[$"Smtp:Domain"]!;
    
    private string _smtpUsername => _configuration[$"Smtp:Username"]!;
    
    private string _smtpPassword => _configuration[$"Smtp:Password"]!;
    
    private int _smtpPort => int.Parse(_configuration[$"Smtp:Port"]!);
    
    private string _smtpEmail => _configuration[$"Smtp:Email"]!;
    
    private bool _smtpEnableSsl => bool.Parse(_configuration[$"Smtp:EnableSsl"]!);
    
    private readonly LinkGenerator _linkGenerator;

    public EmailSender(IConfiguration configuration)
    {
        _configuration = configuration;
        _linkGenerator = new LinkGenerator(configuration);
    }

    public void SendMessage(string emailAddress, string subject, string body)
    {
        try
        {
            using SmtpClient smtpClient = new(_smtpServerDomain);
            
                smtpClient.Port = _smtpPort;
                smtpClient.Credentials = new NetworkCredential(_smtpEmail, _smtpPassword);
                smtpClient.EnableSsl = _smtpEnableSsl;

            using MailMessage message = new();
                
                message.From = new MailAddress(_smtpEmail);
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = true;

            message.To.Add(emailAddress);
            smtpClient.Send(message);
            
            Logger.Log($"Verification email sent to '{emailAddress}'");
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception thrown while sending email to '{emailAddress}': {ex.Message}'", LogLevel.Error);
        }
    }
    
    public void SendVerificationMessage(string emailAddress, string jwt, HttpContext httpContext)
    {
        string link = _linkGenerator.GenerateVerificationLink(httpContext, jwt);
        
        SendMessage(
            emailAddress,
            "Ověření emailu na NČS",
            "Dobrý den, na této emailové adrese došlo k registraci na portálu Nářečí Českých Strnadů. " +
            $"Registraci můžete dokončit kliknutím <a href=\"{link}\">sem</a><br><br> " +
            "Pokud jste o této akci nevíte můžete tento email ignorovat."
        );
        
        Logger.Log($"Sended verification email to address '{emailAddress}'");
    }

    public void SendPasswordResetMessage(string emailAddress, string jwt, HttpContext httpContext)
    {
        string link = _linkGenerator.GeneratePasswordResetLink(jwt);

        SendMessage(
            emailAddress,
            "Nářečí Českých Strnadů - Změna hesla",
            "Dostali jsme žádost o změnu hesla na portálu Nářečí Českých Strnadů. \n " +
            $"Změnu můžete provést <a href={link}>zde</a>. \n \n " +
            $"Pokud jste změnu neudělali vy můžete tento email ignorovat."
        );
        
        Logger.Log($"Sended password reset email to address '{emailAddress}'");
    }
}