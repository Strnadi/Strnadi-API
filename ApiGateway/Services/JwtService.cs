using System.Security.Claims;
using System.Text;
using Models.Database;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Shared.Logging;
using LogLevel = Shared.Logging.LogLevel;

namespace ApiGateway.Services;

internal interface IJwtService
{
    string GenerateToken(string subject);
    bool TryValidateToken(string token, out int? userId);
} 

internal class JwtService : IJwtService
{
    private IConfiguration _configuration;
    
    private string _secretKey => _configuration["$JWT_SECRET_KEY"] ?? throw new NullReferenceException("Invalid configuration key passed");
    
    private string _issuer => _configuration["$JWT_ISSUER"] ?? throw new NullReferenceException("Invalid configuration key passed");
    
    private string _audience => _configuration["$JWT_AUDIENCE"] ?? throw new NullReferenceException("Invalid configuration key passed");
    
    private string _lifetime => _configuration["$JWT_LIFETIME"] ?? throw new NullReferenceException("Invalid configuration key passed");
    
    private TimeSpan _lifetimeAsTimeSpan => TimeSpan.Parse(_lifetime);
    private DateTime _expiresAt => DateTime.UtcNow.Add(_lifetimeAsTimeSpan);
    
    private const string security_algorithm = SecurityAlgorithms.HmacSha256;

    private readonly SecurityKey _securityKey;
    
    internal JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
        _securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
    }

    public string GenerateToken(string subject)
    {
        var tokenHandler = new JwtSecurityTokenHandler();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim(JwtRegisteredClaimNames.Iss, _issuer),
                new Claim(JwtRegisteredClaimNames.Aud, _audience),
            ]),
            SigningCredentials = new SigningCredentials(_securityKey, security_algorithm),
            Expires = _expiresAt
        };

        SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public bool TryValidateToken(string token, out int? userId)
    {
        return TryValidateToken(token, out userId, GetUserId);
    }
    
    private bool TryValidateToken<T>(string token, out T? value, Func<string, T?> extractor)
    {
        if (Validate(token))
        {
            value = extractor(token);

            if (value is null)
            {
                Logger.Log("Failed to read email from validated token.");
                return false;
            }

            Logger.Log("JWT token validated successfully.");
            return true;
        }
        
        Logger.Log("Failed to validate JWT token.");

        value = default;
        return false;
    }
    
    private int? GetUserId(string token)
    {
        string? userIdStr = GetClaims(token).FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        return userIdStr is null ? null : int.Parse(userIdStr);
    }

    private bool Validate(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _securityKey,
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            tokenHandler.ValidateToken(token, validationParameters, out _);
            return true;
        }
        catch (SecurityTokenException ex)
        {
            Logger.Log($"Failed to validate JWT token: {ex.Message}");
            return false;
        }
        catch (Exception e)
        {
            Logger.Log($"An exception thrown during JWT token validation: {e.Message}", LogLevel.Warning);
            return false;
        }
    }

    private Claim[] GetClaims(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        JwtSecurityToken? decodedToken = tokenHandler.ReadJwtToken(token);
        
        return decodedToken.Claims.ToArray();
    }
}