using Microsoft.Extensions.Configuration;
using Tenant.Domain.Configuration;

namespace Tenant.Infrastructure.Configuration;

public class OpenIddictSettings(IConfiguration configuration) : IOpenIddictSettings
{
    public string ClientSecret => configuration["OpenIddict:ClientSecret"]
        ?? throw new InvalidOperationException("OpenIddict:ClientSecret is not configured");
}
