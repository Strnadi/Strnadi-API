using Strnadi.Domain.Configuration;
using Strnadi.Domain.Services;

namespace Strnadi.Infrastructure.MapyCz;

public class MapyCzProxyService(HttpClient httpClient, IMapyCzSettings settings) : IMapyCzProxyService
{
    public async Task<MapyCzProxyResponse> ForwardAsync(string path, string? queryString, CancellationToken cancellationToken = default)
    {
        var targetUrl = $"https://api.mapy.cz/{path}{queryString}";

        using var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
        request.Headers.Add("X-Mapy-Api-Key", settings.Key);

        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        return new MapyCzProxyResponse((int)response.StatusCode, contentType, stream);
    }
}
