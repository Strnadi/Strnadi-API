using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Utils;

[ApiController]
[Route("map")]
public class MapController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public MapController(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    [HttpGet("{*path}")]
    public async Task<IActionResult> ForwardToMapyCz([FromRoute] string path)
    {
        var query = Request.QueryString.Value;
        var targetUrl = $"https://api.mapy.cz/{path}{query}";

        using var client = new HttpClient();

        var req = new HttpRequestMessage(HttpMethod.Get, targetUrl);
        req.Headers.Add("X-Mapy-Api-Key", _configuration["MapyCzKey"]);

        var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        Response.RegisterForDispose(res);

        if (!res.IsSuccessStatusCode)
        {
            return StatusCode((int)res.StatusCode, await res.Content.ReadAsStringAsync());
        }

        var stream = await res.Content.ReadAsStreamAsync();
        var contentType = res.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

        return new FileStreamResult(stream, contentType);
    }
}