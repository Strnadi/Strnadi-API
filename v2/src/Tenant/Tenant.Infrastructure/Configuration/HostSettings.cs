using Microsoft.Extensions.Configuration;
using Tenant.Domain.Configuration;

namespace Tenant.Infrastructure.Configuration;

public class HostSettings(IConfiguration configuration) : IHostSettings
{
    public string ApiHost => configuration["Host"] 
        ?? throw new InvalidOperationException("Host is not configured");
}