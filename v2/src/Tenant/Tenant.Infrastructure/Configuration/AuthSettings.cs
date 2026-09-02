using Tenant.Domain.Configuration;

namespace Tenant.Infrastructure.Configuration;

public class AuthSettings(
    IAppleAuthSettings appleAuthSettings,
    IGoogleAuthSettings googleAuthSettings) : IAuthSettings
{
    public IAppleAuthSettings AppleAuthSettings => appleAuthSettings;

    public IGoogleAuthSettings GoogleAuthSettings => googleAuthSettings;
}
