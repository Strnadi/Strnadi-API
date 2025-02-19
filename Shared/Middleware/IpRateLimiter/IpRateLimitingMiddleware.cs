using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Http;

namespace Shared.Middleware.IpRateLimiter;

public class IpRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;

    private const int requests_limit = 100;
    
    public IpRateLimitingMiddleware(RequestDelegate next, IMemoryCache cache)
    {
        _next = next;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string? ip = context.Connection.RemoteIpAddress?.ToString();
        if (ip is null)
        {
            await _next(context);
            return;
        }

        var cacheKey = $"ip:{ip}";
        if (!_cache.TryGetValue(cacheKey, out int requestsCount))
        {
            requestsCount = 0;
        }

        requestsCount++;

        var cacheEntryOptions = new MemoryCacheEntryOptions()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
        };

        _cache.Set(cacheKey, requestsCount, cacheEntryOptions);

        if (requestsCount > requests_limit)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsync("Too many requests");
            return;
        }

        await _next(context);
    }
}