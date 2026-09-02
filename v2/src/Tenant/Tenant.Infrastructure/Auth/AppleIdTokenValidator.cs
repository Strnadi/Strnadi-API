using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Tenant.Domain.Configuration;
using Tenant.Domain.Services;

namespace Tenant.Infrastructure.Auth;

public class AppleIdTokenValidator : IAppleIdTokenValidator
{
    private const string Issuer = "https://appleid.apple.com";

    private readonly IAppleAuthSettings _appleAuthSettings;
    private readonly ILogger<AppleIdTokenValidator> _logger;

    // ConfigurationManager caches Apple's JWKS internally (default refresh: 24h) and must
    // be reused across calls, otherwise every sign-in re-fetches the discovery document.
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configurationManager;

    public AppleIdTokenValidator(IAppleAuthSettings appleAuthSettings, ILogger<AppleIdTokenValidator> logger)
    {
        _appleAuthSettings = appleAuthSettings;
        _logger = logger;
        _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{Issuer}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever());
    }

    public async Task<AppleIdTokenPayload?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var oidcConfig = await _configurationManager.GetConfigurationAsync(cancellationToken);

            var validationParameters = new TokenValidationParameters
            {
                ValidIssuer = Issuer,
                ValidAudiences = [_appleAuthSettings.ClientIdIos, _appleAuthSettings.ClientIdWeb],
                IssuerSigningKeys = oidcConfig.SigningKeys,
                ValidateLifetime = true
            };

            var handler = new JwtSecurityTokenHandler();
            handler.InboundClaimTypeMap.Clear();
            handler.ValidateToken(idToken, validationParameters, out var validatedToken);

            var jwt = (JwtSecurityToken)validatedToken;
            var subject = jwt.Claims.First(c => c.Type == "sub").Value;
            var email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;

            return new AppleIdTokenPayload(subject, email);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to validate Apple ID token");
            return null;
        }
    }
}
