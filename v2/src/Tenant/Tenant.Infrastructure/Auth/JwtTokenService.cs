using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Tenant.Domain.Configuration;
using Tenant.Domain.Services;

namespace Tenant.Infrastructure.Auth;

public class JwtTokenService(IJwtSettings jwtSettings) : ITokenService
{
    public string GenerateToken(int userId, string email, string role)
    {
        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.SecretKey));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("role", role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings.Issuer,
            audience: jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(jwtSettings.Lifetime),
            signingCredentials: new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public bool ValidateToken(string token, out int userId, bool validateLifetime = true)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = validateLifetime,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var principal = handler.ValidateToken(token, validationParameters, out _);
            userId = int.Parse(principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
            return true;
        }
        catch (Exception)
        {
            userId = default;
            return false;
        }
    }
}