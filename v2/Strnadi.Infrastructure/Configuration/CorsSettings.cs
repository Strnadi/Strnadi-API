using Microsoft.Extensions.Configuration;
using Strnadi.Domain.Configuration;

namespace Strnadi.Infrastructure.Configuration;

public class CorsSettings(IConfiguration configuration) : ICorsSettings
{
    public string Default => configuration["CORS:Default"]
        ?? throw new InvalidOperationException("CORS:Default is not configured");
}
