using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Shared.Middleware.IpRateLimiter;

public class JwtRateLimitingMiddleware
{
    private RequestDelegate _next;
    
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;

    public JwtRateLimitingMiddleware(RequestDelegate next,
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger logger)
    {
        _next = next;
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        
    }
}