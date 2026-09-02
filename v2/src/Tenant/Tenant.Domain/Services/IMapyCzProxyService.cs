namespace Tenant.Domain.Services;

public interface IMapyCzProxyService
{
    Task<MapyCzProxyResponse> ForwardAsync(string path, string? queryString, CancellationToken cancellationToken = default);
}

public record MapyCzProxyResponse(int StatusCode, string ContentType, Stream Content);
