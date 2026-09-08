using Administration.Domain.Configuration;
using Microsoft.Extensions.Configuration;

namespace Administration.Infrastructure.Configuration;

public class AppleAuthSettings(IConfiguration configuration) : IAppleAuthSettings
{
    public string ClientId => configuration["Auth:Apple:ClientId"]
        ?? throw new InvalidOperationException("Auth:Apple:ClientId is not configured");

    public string TeamId => configuration["Auth:Apple:TeamId"]
        ?? throw new InvalidOperationException("Auth:Apple:TeamId is not configured");

    public string KeyId => configuration["Auth:Apple:KeyId"]
        ?? throw new InvalidOperationException("Auth:Apple:KeyId is not configured");

    public string P8PrivateKey => configuration["Auth:Apple:P8PrivateKey"]
        ?? throw new InvalidOperationException("Auth:Apple:P8PrivateKey is not configured");
}
