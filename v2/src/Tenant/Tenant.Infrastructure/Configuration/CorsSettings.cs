using Microsoft.Extensions.Configuration;
using Tenant.Domain.Configuration;

namespace Tenant.Infrastructure.Configuration;

public class CorsSettings(IConfiguration configuration) : ICorsSettings
{
    public string Default => configuration["CORS:Default"]
        ?? throw new InvalidOperationException("CORS:Default is not configured");
}
