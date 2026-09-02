using Microsoft.AspNetCore.Mvc;
using Strnadi.Domain.Services;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("map")]
public class MapController(IMapyCzProxyService mapsProxy) : ControllerBase
{
    [HttpGet("{*path}")]
    public async Task<IActionResult> ForwardAsync([FromRoute] string path, CancellationToken cancellationToken)
    {
        var query = Request.QueryString.Value;
        
        var proxy = await mapsProxy.ForwardAsync(path, query, cancellationToken);

        if (proxy.StatusCode is < 200 or > 299)
        {
            using var reader = new StreamReader(proxy.Content);
            var errorBody = await reader.ReadToEndAsync(cancellationToken);
            return StatusCode(proxy.StatusCode, errorBody);
        }

        return File(proxy.Content, proxy.ContentType);
    }
}
