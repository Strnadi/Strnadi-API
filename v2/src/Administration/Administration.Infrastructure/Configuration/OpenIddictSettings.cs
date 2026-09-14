using Administration.Domain.Configuration;
using Microsoft.Extensions.Configuration;

namespace Administration.Infrastructure.Configuration;

public class OpenIddictSettings(IConfiguration configuration) : IOpenIddictSettings
{
    public string TenantClientSecret => configuration["OpenIddict:TenantClientSecret"] 
        ?? throw new InvalidOperationException("OpenIddict:TenantClientSecret is not configured");
}