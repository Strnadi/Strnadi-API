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

using ApiGateway.Services;
using Microsoft.AspNetCore.Mvc;
using Models.Requests;
using Shared.Communication;
using Shared.Extensions;
using LinkGenerator = ApiGateway.Services.LinkGenerator;

namespace ApiGateway.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    private readonly JwtService _jwtService;

    public AuthController(IConfiguration config, JwtService jwtService)
    {
        _configuration = config;
        _jwtService = jwtService;
    }

    [HttpGet]
    public IActionResult VerifyJwt()
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!_jwtService.TryValidateToken(jwt, out _))
            return Unauthorized();

        return Ok();
    }

    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request,
        [FromServices] DagUsersControllerClient client)
    {
        HttpRequestResult? response = await client.AuthorizeUserAsync(request);

        if (response is null || !response.Success)
            return await this.HandleErrorResponseAsync(response);

        string jwt = _jwtService.GenerateToken(request.Email);

        return Ok(jwt);
    }
    
    [HttpPost("sign-up")]
    public async Task<IActionResult> SignUpAsync([FromBody] SignUpRequest request,
        [FromServices] DagUsersControllerClient client)
    {
        HttpRequestResult? response = await client.SignUpAsync(request);

        if (response is null)
            return await this.HandleErrorResponseAsync(response);

        string jwt = _jwtService.GenerateToken(request.Email);
        
        SendVerificationMessageAsync(request.Email, jwt);

        return Ok(jwt);
    }

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyEmail([FromServices] DagUsersControllerClient client)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!_jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        HttpRequestResult? response = await client.VerifyUser(email!);

        bool success = response?.Success is not null && response.Success;

        string redirectionPage = new LinkGenerator(_configuration).GenerateEmailVerificationRedirectionLink(success);

        return RedirectPermanent(redirectionPage);
    }

    [HttpGet("request-password-reset")]
    public IActionResult RequestPasswordReset([FromQuery] string email)
    {
        string jwt = _jwtService.GenerateToken(email);
        
        SendPasswordResetMessageAsync(email, jwt);

        return Ok();
    }
    
    [HttpPatch("change-password")]
    public async Task<IActionResult> ChangePasswordAsync(
        [FromBody] ChangePasswordRequest request,
        [FromServices] DagUsersControllerClient client)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!_jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        request.Email = email;

        HttpRequestResult? response = await client.ChangePassword(request);

        if (response is null || !response.Success)
            return await this.HandleErrorResponseAsync(response);
        
        return Ok();
    }   
    
    private void SendVerificationMessageAsync(string emailAddress, string jwt)
    {
        var emailSender = new EmailSender(_configuration);
        Task.Run(() =>
            emailSender.SendVerificationMessage(emailAddress, jwt, HttpContext));
    }

    private void SendPasswordResetMessageAsync(string emailAddress, string jwt)
    {
        var emailSender = new EmailSender(_configuration);
        Task.Run(() => 
            emailSender.SendPasswordResetMessage(emailAddress, jwt, HttpContext));
    }
}