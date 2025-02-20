using Microsoft.AspNetCore.Http;

namespace Shared.Extensions;

public static class HttpContextExtensions
{
    public static string? GetJwt(this HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader)) 
            return null;
        
        var bearerToken = authHeader.ToString();
        
        return bearerToken.StartsWith("Bearer ",
            StringComparison.OrdinalIgnoreCase)
            ? bearerToken.Substring("Bearer ".Length)
                .Trim()
            : null;
    }
}