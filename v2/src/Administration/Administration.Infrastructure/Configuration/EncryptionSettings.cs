using Administration.Domain.Configuration;
using Microsoft.Extensions.Configuration;

namespace Administration.Infrastructure.Configuration;

public class EncryptionSettings(IConfiguration configuration) : IEncryptionSettings
{
    public string Key => configuration["Encryption:Key"]
        ?? throw new InvalidOperationException("Encryption:Key is not configured");
}
