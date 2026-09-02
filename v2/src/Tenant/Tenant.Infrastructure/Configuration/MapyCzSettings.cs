using Microsoft.Extensions.Configuration;
using Tenant.Domain.Configuration;

namespace Tenant.Infrastructure.Configuration;

public class MapyCzSettings(IConfiguration configuration) : IMapyCzSettings
{
    public string Key => configuration["MapyCz:Key"]
        ?? throw new InvalidOperationException("MapyCz:Key is not configured");
}
