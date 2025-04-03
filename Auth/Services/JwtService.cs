/*
 * Copyright (C) 2024 Stanislav Motsnyi
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Shared.Logging;
using LogLevel = Shared.Logging.LogLevel;

namespace Auth.Services;

public class JwtService
{
    private readonly IConfiguration _configuration;
    
    private string _secretKey => _configuration["Jwt:SecretKey"] ?? throw new NullReferenceException("Invalid configuration key passed");
    
    private string _issuer => _configuration["Jwt:Issuer"] ?? throw new NullReferenceException("Invalid configuration key passed");
    
    private string _audience => _configuration["Jwt:Audience"] ?? throw new NullReferenceException("Invalid configuration key passed");
    
    private string _lifetime => _configuration["Jwt:Lifetime"] ?? throw new NullReferenceException("Invalid configuration key passed");
    
    private TimeSpan _lifetimeAsTimeSpan => TimeSpan.Parse(_lifetime);
    private DateTime _expiresAt => DateTime.UtcNow.Add(_lifetimeAsTimeSpan);
    
    private const string security_algorithm = SecurityAlgorithms.HmacSha256;

    private readonly SecurityKey _securityKey;

    private const string permission_claim = "permissions";
    
    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
        _securityKey = new SymmetricSecurityKey(Convert.FromBase64String(_secretKey));
    }

    public string GenerateRegularToken(string subject) =>
        GenerateToken([
            new Claim(JwtRegisteredClaimNames.Sub, subject),
            new Claim(permission_claim, TokenPermissions.Regular.ToString())
        ]);

    public string GenerateLimitedToken(string subject) =>
        GenerateToken([
            new Claim(JwtRegisteredClaimNames.Sub, subject),
            new Claim(permission_claim, TokenPermissions.Limited.ToString())
        ]);

    private string GenerateToken(Claim[] claims)
    {
        var tokenHandler = new JwtSecurityTokenHandler();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(_securityKey, security_algorithm),
            Expires = _expiresAt,
            Issuer = _issuer,
            Audience = _audience,
            AdditionalHeaderClaims = _securityKey.KeyId != null
                ? new Dictionary<string, object> { { "kid", _securityKey.KeyId } }
                : null,
        };

        SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
    
    public bool TryValidateLimitedToken(string? jwt, out string? email)
    {
        return TryValidateToken(jwt, out email, GetEmail);
    }
    
    public bool TryValidateRegularToken(string? token, out string? email)
    {
        bool validated = TryValidateToken(token, out email, GetEmail);

        if (!validated) return false;

        return GetPermissions(email!) == TokenPermissions.Regular;
    }
    
    private bool TryValidateToken<T>(string? token, out T? value, Func<string, T?> extractor)
    {
        if (token is null)
        {
            value = default;
            return false;
        }
        
        if (Validate(token))
        {
            value = extractor(token);
            return value is not null;
        }

        value = default;
        return false;
    }

    public string? GetEmail(string token)
    {
        string? emailStr = GetClaims(token)!.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        return emailStr;
    }

    private bool Validate(string token)
    {
        Logger.Log("Validated JWT token: " + token);
        
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

        try
        {
            var decodedToken = tokenHandler.ReadJwtToken(token);
            return decodedToken.Claims.ToArray();
        }
        catch (SecurityTokenException ex)
        {
            Logger.Log($"Failed to validate JWT token: {ex.Message}");
            throw;
        }
    }

    private TokenPermissions GetPermissions(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var decodedToken = tokenHandler.ReadJwtToken(token);
            var permission = decodedToken.Claims.Where(c => c.Type == permission_claim)
                .Select(c => c.Value)
                .FirstOrDefault();
            return permission is null ? 0 : Enum.Parse<TokenPermissions>(permission);
        }
        catch (SecurityTokenException ex)
        {
            Logger.Log($"Failed to validate JWT token: {ex.Message}");
            return 0;
        }
    }
}