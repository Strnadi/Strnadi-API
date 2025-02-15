using System.Text;
using ApiGateway.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Logging;
using LogLevel = Shared.Logging.LogLevel;

namespace ApiGateway.Controllers;

[ApiController]
[Route("/users")]
public class UsersController : ControllerBase
{
    private readonly IConfiguration _configuration;

    private readonly HttpClient _httpClient;
    
    private readonly IJwtService _jwtService;
    
    private string _dagCntName => _configuration["MSAddresses:DagName"] ?? throw new NullReferenceException("Failed to load microservice name");
    private string _dagCntPort => _configuration["MSAddresses:DagPort"] ?? throw new NullReferenceException("Failed to load microservice port");
    
    private const string dag_get_endpoint = "users/get";
    
    public UsersController(IConfiguration config)
    {
        _configuration = config;
        _httpClient = new HttpClient();
        _jwtService = new JwtService(config);
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string jwt)
    {
        if (!_jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        string dagUrl = $"http://{_dagCntName}:{_dagCntPort}/{dag_get_endpoint}";
        
        var content = new StringContent(email!, Encoding.UTF8, "text/plain");

        try
        {
            var response = await _httpClient.PostAsync(dagUrl, content);
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception caught while communicating with DAG module: {ex.Message}", LogLevel.Error);
            return StatusCode(500, ex.Message);
        }
    } 
}