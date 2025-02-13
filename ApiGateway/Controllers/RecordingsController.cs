using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Models.Requests;
using Shared.Logging;

namespace ApiGateway.Controllers;

[ApiController]
[Route("recordings")]
public class RecordingsController : ControllerBase
{
    private readonly IConfiguration _configuration;

    private readonly HttpClient _httpClient;
    
    private string _dagCntName => _configuration["MSAddresses:DagName"] ?? throw new NullReferenceException("Failed to load microservice name");
    private string _dagCntPort => _configuration["MSAddresses:DagPort"] ?? throw new NullReferenceException("Failed to load microservice port");

    private const string dag_uploadRec_endpoint = "recordings/upload";
    private const string dag_uploadRecPart_endpoint = "recordings/upload-part";
    
    public RecordingsController(IConfiguration config)
    {
        _configuration = config;
        _httpClient = new HttpClient();
    }
    
    [HttpPost("recordings/upload")]
    public async Task<IActionResult> Upload([FromBody] RecordingUploadReq request)
    {
        string dagUrl = $"http://{_dagCntName}:{_dagCntPort}/{dag_uploadRec_endpoint}";

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(dagUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Log($"Recording upload failed with status code {response.StatusCode}");
                return StatusCode((int)response.StatusCode);
            }
            
            
        }
        // must return id of generated recording
    }

    [HttpPost("recordings/upload-part")]
    public async Task<IActionResult> UploadPart([FromBody] RecordingUploadReq request)
    {
        
    }
}