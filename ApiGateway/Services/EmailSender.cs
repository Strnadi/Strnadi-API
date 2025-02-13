using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using Shared.Logging;

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

    public EmailSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void SendMessage(string emailAddress, string subject, string body)
    {
        try
        {
            SmtpClient smtpClient = new(_smtpServerDomain)
            {
                Port = _smtpPort,
                Credentials = new NetworkCredential(_smtpEmail, _smtpPassword),
                EnableSsl = true
            };

            MailMessage message = new()
            {
                From = new MailAddress(_smtpEmail),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            message.To.Add(emailAddress);
            smtpClient.Send(message);
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception thrown while sending email to {emailAddress}: {ex.Message}'");
        }
        
    }
    
    public void SendVerificationMessage(HttpContext httpContext, ControllerContext controllerContext, string emailAddress, string jwt)
    {
        string link = new LinkGenerator().GenerateLink(jwt, httpContext, controllerContext);
        
        SendMessage(
            emailAddress,
            "Confirm your email in Navrat krale - Nareci ceskych strnadu",
            "Please confirm you email by clicking this link: " + link
            );
    }
}