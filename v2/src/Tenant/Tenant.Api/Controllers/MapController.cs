using Microsoft.AspNetCore.Mvc;
using Tenant.Domain.Services;

namespace Tenant.Api.Controllers;

[ApiController]
[Route("map")]
public class MapController(IMapyCzProxyService mapsProxy) : ControllerBase
{
    /// <summary>Proxies a request to Mapy.cz, attaching our API key. Non-2xx upstream responses are passed through as-is, so the status code isn't fixed.</summary>
    [HttpGet("{*path}")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
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
