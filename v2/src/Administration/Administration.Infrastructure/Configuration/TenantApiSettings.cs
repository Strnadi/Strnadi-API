using Administration.Domain.Configuration;
using Microsoft.Extensions.Configuration;

namespace Administration.Infrastructure.Configuration;

public class TenantApiSettings(IConfiguration configuration) : ITenantApiSettings
{
    public string BaseUrl => configuration["TenantApi:BaseUrl"]?.TrimEnd('/')
        ?? throw new InvalidOperationException("TenantApi:BaseUrl is not configured");
}
