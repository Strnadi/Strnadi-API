using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Shared.Middleware.Logging;

public class LoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;
    
    public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);
        Console.WriteLine();
        Console.WriteLine($"[{DateTime.Now}]");
        Console.WriteLine();
        _logger.LogInformation($"Request to {context.Request.Path} from {context.Connection.RemoteIpAddress} with code {context.Response.StatusCode}");
    }
}