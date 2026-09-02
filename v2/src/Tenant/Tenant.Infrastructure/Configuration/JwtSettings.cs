using Microsoft.Extensions.Configuration;
using Tenant.Domain.Configuration;

namespace Tenant.Infrastructure.Configuration;

public class JwtSettings(IConfiguration configuration) : IJwtSettings
{
    public string SecretKey => configuration["Jwt:SecretKey"]
        ?? throw new InvalidOperationException("Jwt:SecretKey is not configured");

    public string Issuer => configuration["Jwt:Issuer"]
        ?? throw new InvalidOperationException("Jwt:Issuer is not configured");

    public string Audience => configuration["Jwt:Audience"]
        ?? throw new InvalidOperationException("Jwt:Audience is not configured");

    public TimeSpan Lifetime => TimeSpan.Parse(configuration["Jwt:Lifetime"]
        ?? throw new InvalidOperationException("Jwt:Lifetime is not configured"));
}
