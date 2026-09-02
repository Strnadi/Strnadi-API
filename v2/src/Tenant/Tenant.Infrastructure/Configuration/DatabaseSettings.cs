using Microsoft.Extensions.Configuration;
using Tenant.Domain.Configuration;

namespace Tenant.Infrastructure.Configuration;

public class DatabaseSettings(IConfiguration configuration) : IDatabaseSettings
{
    public string ConnectionString => configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured");
}
