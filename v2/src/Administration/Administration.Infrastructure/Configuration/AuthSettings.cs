using Administration.Domain.Configuration;

namespace Administration.Infrastructure.Configuration;

public class AuthSettings(
    IGoogleAuthSettings googleAuthSettings,
    IAppleAuthSettings appleAuthSettings) : IAuthSettings
{
    public IGoogleAuthSettings GoogleAuthSettings => googleAuthSettings;

    public IAppleAuthSettings AppleAuthSettings => appleAuthSettings;
}
