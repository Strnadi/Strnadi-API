using Administration.Domain.Configuration;
using Microsoft.Extensions.Configuration;

namespace Administration.Infrastructure.Configuration;

public class GoogleAuthSettings(IConfiguration configuration) : IGoogleAuthSettings
{
    public string ClientId => configuration["Auth:Google:ClientId"]
        ?? throw new InvalidOperationException("Auth:Google:ClientId is not configured");

    public string ClientSecret => configuration["Auth:Google:ClientSecret"]
        ?? throw new InvalidOperationException("Auth:Google:ClientSecret is not configured");
}
