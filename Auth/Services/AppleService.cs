using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text.Json;

// strongly type your config
public sealed class AppleAuthOptions
{
    public required string TeamId { get; init; }
    public required string KeyId { get; init; }
    public required string ClientIdWeb { get; init; }     // Services ID e.g. com.your.app.web
    public required string ClientIdIos { get; init; }     // iOS bundle id if you also handle mobile
    public required string RedirectUriWeb { get; init; }  // must match Apple config
    public required string P8PrivateKey { get; init; }    // PEM contents (keep secret)
}

public sealed class AppleTokenResponse
{
    public string? access_token { get; set; }
    public string? id_token { get; set; }
    public string? refresh_token { get; set; }
    public string? token_type { get; set; }
    public int? expires_in { get; set; }
}

public static class AppleAuth
{
    public static async Task<string> CreateClientSecretAsync(AppleAuthOptions opt)
    {
        // ES256 signing of client secret JWT
        using var ecdsa = ECDsa.Create();
        // Import PKCS8 from PEM
        ecdsa.ImportFromPem(opt.P8PrivateKey.AsSpan());

        var securityKey = new ECDsaSecurityKey(ecdsa) { KeyId = opt.KeyId };
        var creds = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);

        var now = DateTimeOffset.UtcNow;
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.CreateJwtSecurityToken(
            issuer: opt.TeamId,
            audience: "https://appleid.apple.com",
            subject: new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim("sub", opt.ClientIdWeb) // we’ll override per flow if needed
            }),
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(30).UtcDateTime,
            issuedAt: now.UtcDateTime,
            signingCredentials: creds);
        return handler.WriteToken(jwt);
    }

    public static async Task<AppleTokenResponse> ExchangeCodeAsync(HttpClient http, AppleAuthOptions opt, string code, string clientId, string redirectUri)
    {
        var clientSecret = await CreateClientSecretAsync(opt);

        var body = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string,string>("grant_type","authorization_code"),
            new KeyValuePair<string,string>("code", code),
            new KeyValuePair<string,string>("client_id", clientId),
            new KeyValuePair<string,string>("client_secret", clientSecret),
            new KeyValuePair<string,string>("redirect_uri", redirectUri),
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://appleid.apple.com/auth/token") { Content = body };
        using var res = await http.SendAsync(req);
        var json = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception($"Apple token exchange failed: {json}");
        return JsonSerializer.Deserialize<AppleTokenResponse>(json)!;
    }

    public static async Task<JwtSecurityToken> ValidateIdTokenAsync(string idToken, string expectedAudience)
    {
        var handler = new JwtSecurityTokenHandler();
        var configManager = new Microsoft.IdentityModel.Protocols.ConfigurationManager<Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration>(
            "https://appleid.apple.com/.well-known/openid-configuration",
            new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfigurationRetriever());

        var oidc = await configManager.GetConfigurationAsync(CancellationToken.None);
        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = "https://appleid.apple.com",
            ValidAudience = expectedAudience,
            IssuerSigningKeys = oidc.SigningKeys,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true
        };

        handler.ValidateToken(idToken, validationParameters, out var validated);
        return (JwtSecurityToken)validated;
    }
}