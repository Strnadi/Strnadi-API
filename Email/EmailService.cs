using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Logging;

namespace Email;

public class EmailService
{
    public void SendEmailVerificationMessage(string emailAddress,
        string? nickname,
        string jwt,
        HttpContext httpContext,
        [FromServices] LinkGenerator linkGenerator,
        [FromServices] EmailSender emailSender)
    {
        string link = linkGenerator.GenerateVerificationLink(httpContext, jwt);
        
        emailSender.SendMessage(
            emailAddress,
            subject: "Nářečí českých strnadů – potvrzení nového uživatele",
            body: $"""
                  Děkujeme za zájem o projekt občanské vědy Nářečí českých strnadů.
                  
                  Registraci nového uživatele {(nickname != null ? $"s přezdívkou <strong>{nickname}</strong>" : "")} potvrdíte kliknutím na <a href='{link}'>tento link</a>.
                  Pokud jste se do projektu neregistrovali nebo jste zadali tuto e-mailovou adresu omylem, zprávu ignorujte.
                  
                  Vaši strnadi 
                  
                  <a href='https://www.strnadi.cz'>www.strnadi.cz</a>
                  """
        );
        
        Logger.Log($"Sended verification email to address '{emailAddress}'");
    }

    public void SendPasswordResetMessage(string emailAddress,
        string? nickname,
        string jwt,
        [FromServices] LinkGenerator linkGenerator,
        [FromServices] EmailSender emailSender)
    {
        string link = linkGenerator.GeneratePasswordResetLink(jwt);

        emailSender.SendMessage(
            emailAddress,
            subject: "Nářečí českých strnadů – zapomenuté heslo",
            body: $"""
                   Nové heslo pro váš uživatelský účet v projektu Nářečí českých strnadů {(nickname != null ? $"s přezdívkou <strong>{nickname}</strong>" : "")} můžete zvolit online po kliknutí na <a href='{link}'>tento link</a>. 
                   Pokud heslo měnit nechcete, zprávu ignorujte. 
                   
                   Vaši strnadi 
                   
                   <a href='https://www.strnadi.cz'>www.strnadi.cz</a>
                   """
        );
        
        Logger.Log($"Sended password reset email to address '{emailAddress}'");
    }
}