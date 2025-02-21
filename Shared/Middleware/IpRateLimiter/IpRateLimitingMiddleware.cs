/*
 * Copyright (C) 2024 Stanislav Motsnyi
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Shared.Middleware.IpRateLimiter;

public class IpRateLimitingMiddleware
{
    private readonly RequestDelegate _next;

    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;

    private int requestsLimit =>
        int.Parse(_configuration["RequestLimiting:Limit"] ??
                  throw new NullReferenceException("Invalid configuration key passed"));
    private TimeSpan timeLimit =>
        TimeSpan.Parse(_configuration["RequestLimiting:Period"] ??
                       throw new NullReferenceException("Invalid configuration key passed"));
    
    public IpRateLimitingMiddleware(RequestDelegate next,
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<IpRateLimitingMiddleware> logger)
    {
        _next = next;
        
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
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
            AbsoluteExpirationRelativeToNow = timeLimit
        };

        _cache.Set(cacheKey, requestsCount, cacheEntryOptions);

        if (requestsCount > requestsLimit)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsync("Too many requests");
            _logger.LogWarning($"IP {ip} exceeded the rate limit");
            return;
        }

        await _next(context);
    }
}