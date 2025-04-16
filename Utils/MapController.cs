using Microsoft.AspNetCore.Mvc;

namespace Utils;

[ApiController]
[Route("map")]
public class MapController : ControllerBase
{
    [HttpGet("{*path}")]
    public async Task<IActionResult> ForwardToMapyCz([FromRoute] string path)
    {
        var query = Request.QueryString.Value;
        var targetUrl = $"https://api.mapy.cz/{path}{query}";

        using var client = new HttpClient();
        var response = await client.GetAsync(targetUrl);

        var content = await response.Content.ReadAsStringAsync();
        return Content(content, "*/*");
    }
}