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
using System.Text;
using System.Text.Json;
using ApiGateway.Services;
using Microsoft.AspNetCore.Mvc;
using Models.Requests;
using Shared.Logging;
using LogLevel = Shared.Logging.LogLevel;

namespace ApiGateway.Controllers;

[ApiController]
[Route("auth/")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    private readonly HttpClient _httpClient;
    
    private readonly IJwtService _jwtService;
    
    private string _dagCntName => _configuration["MSAddresses:DagName"] ?? throw new NullReferenceException("Failed to load microservice name");
    private string _dagCntPort => _configuration["MSAddresses:DagPort"] ?? throw new NullReferenceException("Failed to load microservice port");
    
    private const string dag_login_endpoint = "users/authorize-user";
    private const string dag_signup_endpoint = "users/sign-up";
    private const string dag_verify_endpoint = "users/verify";
    
    public AuthController(IConfiguration config)
    {
        _configuration = config;
        _httpClient = new HttpClient();
        _jwtService = new JwtService(config);
    }
    
    [HttpPost("/auth/login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
    {
        string dagUrl = $"http://{_dagCntName}:{_dagCntPort}/{dag_login_endpoint}";
        
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(dagUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Log($"Authorization of user '{request.Email}' failed with status '{response.StatusCode}'");
                return StatusCode((int)response.StatusCode);
            }
            
            string jwt = _jwtService.GenerateToken(request.Email);
            
            Logger.Log($"User '{request.Email}' logged in successfully");
            return Ok(jwt);
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception caught while communicating with DAG module: {ex.Message}", LogLevel.Error);
            return StatusCode(500, ex.Message);
        }
    }

    [HttpPost("/auth/sign-up")]
    public async Task<IActionResult> SignUpAsync([FromBody] SignUpRequest request)
    {
        string dagUrl = $"http://{_dagCntName}:{_dagCntPort}/{dag_signup_endpoint}";
        
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(dagUrl, content);
            
            if (!response.IsSuccessStatusCode)
            {
                Logger.Log($"Registration of user '{request.Email}' failed with status '{response.StatusCode}'", LogLevel.Warning);
                return StatusCode((int)response.StatusCode);
            }

            string jwt = _jwtService.GenerateToken(request.Email);
            Logger.Log($"User '{request.Email}' registered successfully");
            
            SendVerificationMessageAsynchronously(request.Email, jwt);

            return Ok(jwt);
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception caught while redirecting recording uploading request to DAG: {ex.Message}", LogLevel.Error);
            return StatusCode(500, ex.Message);
        }
    }

    [HttpPost("/auth/verify")]
    public async Task<IActionResult> VerifyUser([FromQuery] string jwt)
    {
        if (!_jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();
        
        string dagUrl = $"http://{_dagCntName}:{_dagCntPort}/{dag_verify_endpoint}";

        try
        {
            var response = await _httpClient.PostAsync(dagUrl, new StringContent(email!, Encoding.UTF8, "text/plain"));

            if (!response.IsSuccessStatusCode)
            {
                Logger.Log($"Verification of user '{email}' failed with status '{response.StatusCode}'",
                    LogLevel.Warning);
                return StatusCode((int)response.StatusCode);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception caught while redirecting to verifying user: {ex.Message}", LogLevel.Error);
            return StatusCode(500, ex.Message);
        }
    }
    
    private void SendVerificationMessageAsynchronously(string emailAddress, string jwt)
    {
        var emailSender = new EmailSender(_configuration);
        Task.Run(() =>
            emailSender.SendVerificationMessage(HttpContext, ControllerContext, emailAddress, jwt));
    }
} 