using Strnadi.Domain.Configuration;

namespace Strnadi.Infrastructure.Configuration;

public class AuthSettings(
    IAppleAuthSettings appleAuthSettings,
    IGoogleAuthSettings googleAuthSettings) : IAuthSettings
{
    public IAppleAuthSettings AppleAuthSettings => appleAuthSettings;

    public IGoogleAuthSettings GoogleAuthSettings => googleAuthSettings;
}
