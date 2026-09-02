using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Strnadi.Domain.Configuration;
using Strnadi.Domain.Services;

namespace Strnadi.Infrastructure.Auth;

public class GoogleIdTokenValidator(IGoogleAuthSettings googleAuthSettings, ILogger<GoogleIdTokenValidator> logger)
    : IGoogleIdTokenValidator
{
    public async Task<GoogleIdTokenPayload?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [googleAuthSettings.Android, googleAuthSettings.Ios, googleAuthSettings.Web]
            });

            return new GoogleIdTokenPayload(payload.Subject, payload.Email, payload.GivenName, payload.FamilyName);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to validate Google ID token");
            return null;
        }
    }
}
