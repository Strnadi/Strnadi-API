using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Models.Requests.Ai;

namespace Shared.Tools;

public class AiModelConnector
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiModelConnector> _logger;

    public AiModelConnector(HttpClient httpClient, ILogger<AiModelConnector> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PredicationResult?> Classify(byte[] normalizedWav)
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(3600);
        
        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(normalizedWav);

        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(fileContent, "file");

        try
        {
            _logger.LogInformation("Starting classificatiotn");
            var response = await _httpClient.PostAsync("http://classification:8000/classify", form);
            var responseText = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<PredicationResult>(responseText);
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
            return null;
        }
    }
}