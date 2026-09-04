using Microsoft.Extensions.Logging;
using Tenant.Domain.Configuration;
using Tenant.Domain.Services;

namespace Tenant.Infrastructure.MapyCz;

public class MapyCzProxyService(HttpClient httpClient, IMapyCzSettings settings, ILogger<MapyCzProxyService> logger) : IMapyCzProxyService
{
    public async Task<MapyCzProxyResponse> ForwardAsync(string path, string? queryString, CancellationToken cancellationToken = default)
    {
        var targetUrl = $"https://api.mapy.cz/{path}{queryString}";

        using var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
        request.Headers.Add("X-Mapy-Api-Key", settings.Key);

        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
            logger.LogWarning("Mapy.cz upstream returned {StatusCode} for {Path}", (int)response.StatusCode, path);

        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        return new MapyCzProxyResponse((int)response.StatusCode, contentType, stream);
    }
}
