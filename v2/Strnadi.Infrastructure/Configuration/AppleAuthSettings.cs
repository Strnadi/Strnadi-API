using Microsoft.Extensions.Configuration;
using Strnadi.Domain.Configuration;

namespace Strnadi.Infrastructure.Configuration;

public class AppleAuthSettings(IConfiguration configuration) : IAppleAuthSettings
{
    public string TeamId => configuration["Auth:Apple:TeamId"]
        ?? throw new InvalidOperationException("Auth:Apple:TeamId is not configured");

    public string KeyId => configuration["Auth:Apple:KeyId"]
        ?? throw new InvalidOperationException("Auth:Apple:KeyId is not configured");

    public string ClientIdWeb => configuration["Auth:Apple:ClientIdWeb"]
        ?? throw new InvalidOperationException("Auth:Apple:ClientIdWeb is not configured");

    public string ClientIdIos => configuration["Auth:Apple:ClientIdIos"]
        ?? throw new InvalidOperationException("Auth:Apple:ClientIdIos is not configured");

    public string RedirectUriWeb => configuration["Auth:Apple:RedirectUriWeb"]
        ?? throw new InvalidOperationException("Auth:Apple:RedirectUriWeb is not configured");

    public string P8PrivateKey => configuration["Auth:Apple:P8PrivateKey"]
        ?? throw new InvalidOperationException("Auth:Apple:P8PrivateKey is not configured");
}
