using Microsoft.Extensions.Configuration;
using Tenant.Domain.Configuration;

namespace Tenant.Infrastructure.Configuration;

public class EncryptionSettings(IConfiguration configuration) : IEncryptionSettings
{
    public string Key => configuration["Encryption:Key"]
        ?? throw new InvalidOperationException("Encryption:Key is not configured");
}