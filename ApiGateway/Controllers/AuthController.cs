using System.Text;
using System.Text.Json;
using ApiGateway.Services;
using Microsoft.AspNetCore.Mvc;
using Models.Requests;
using Shared.Logging;

namespace ApiGateway.Controllers;

[ApiController]
[Route("/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    private readonly HttpClient _httpClient;
    
    private readonly IJwtService _jwtService;
    
    private string _authCntName => _configuration["MS_AUTH"] ?? throw new NullReferenceException("Failed to load microservice name");
    private string _authCntPort => _configuration["PORT_AUTH"] ?? throw new NullReferenceException("Failed to load microservice port");
    
    private const string dag_login_endpoint = "/users/authorize-user";
    private const string dag_signup_endpoint = "/users/sign-up";
    
    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
        _httpClient = new HttpClient();
        _jwtService = new JwtService(_configuration);
    }
    
    [HttpPost("/auth/login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
    {
        string dagUrl = $"http://{_authCntName}:{_authCntPort}/{dag_login_endpoint}";

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(dagUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Log($"Authorization of user '{request.Email} failed with status code {response.StatusCode}'");
                return StatusCode((int)response.StatusCode);
            }
            
            string jwt = _jwtService.GenerateToken(request.Email);
            Logger.Log($"User '{request.Email}' logged in successfully");
            return Ok(jwt);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    [HttpPost("/auth/sign-up")]
    public async Task<IActionResult> RegisterAsync([FromBody] SignUpRequest request)
    {
        string dagUrl = $"http://{_authCntName}:{_authCntPort}/{dag_signup_endpoint}";
        
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(dagUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Log($"Registration of user '{request.Email}' failed with status code {response.StatusCode}");
                return StatusCode((int)response.StatusCode);
            }

            string jwt = _jwtService.GenerateToken(request.Email);
            Logger.Log($"User '{request.Email}' registered successfully");
            return Ok(jwt);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }
} 