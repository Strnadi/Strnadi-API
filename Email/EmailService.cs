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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Email;

public class EmailService
{
    private readonly IConfiguration _configuration;
    
    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void SendEmailVerificationAsync(string email, int userId,
        string? nickname,
        string jwt)
    {
        LinkGenerator linkGenerator = new LinkGenerator(_configuration);
        EmailSender emailSender = new EmailSender(_configuration);
        
        string link = linkGenerator.GenerateVerificationLink(userId, jwt);
        
        emailSender.SendMessage(
            email,
            subject: "Nářečí českých strnadů – potvrzení nového uživatele",
            body: $"""
                  <p style='font-size:1rem'>
                  Děkujeme za zájem o projekt občanské vědy Nářečí českých strnadů.<br>
                  <br>
                  Registraci nového uživatele {(!string.IsNullOrEmpty(nickname) ? $"s přezdívkou <strong>{nickname}</strong>" : "")} potvrdíte kliknutím na <a href='{link}'>tento link</a>.<br>
                  
                  Pokud jste se do projektu neregistrovali nebo jste zadali tuto e-mailovou adresu omylem, zprávu ignorujte.<br>
                  <br>
                  </p>
                  <h3>Vaši strnadi</h3><br> 
                  <br>
                  <a href='https://www.strnadi.cz'>www.strnadi.cz</a>
                  """
        );
    }

    public void SendPasswordResetMessage(string emailAddress,
        string? nickname,
        string jwt)
    {
        LinkGenerator linkGenerator = new LinkGenerator(_configuration);
        EmailSender emailSender = new EmailSender(_configuration);
        
        string link = linkGenerator.GeneratePasswordResetLink(jwt);
        
        emailSender.SendMessage(
            emailAddress,
            subject: "Nářečí českých strnadů – zapomenuté heslo",
            body: $"""
                   Nové heslo pro váš uživatelský účet v projektu Nářečí českých strnadů {(nickname != null ? $"s přezdívkou <strong>{nickname}</strong>" : "")} můžete zvolit online po kliknutí na <a href='{link}'>tento link</a>. <br>
                   Pokud heslo měnit nechcete, zprávu ignorujte. <br>
                   <br>
                   <h5>Vaši strnadi</h5><br>
                   <br>
                   <a href='https://www.strnadi.cz'>www.strnadi.cz</a>
                   """
        );
    }
}