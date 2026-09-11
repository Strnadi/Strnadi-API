using Microsoft.Extensions.Configuration;
using Platform.Shared.Kernel.Configuration;

namespace Platform.Shared.Infrastructure.Configuration;

public class EncryptionSettings(IConfiguration configuration) : IEncryptionSettings
{
    public string Key => configuration["Encryption:Key"]
        ?? throw new InvalidOperationException("Encryption:Key is not configured");
}
