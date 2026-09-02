namespace Tenant.Domain.Configuration;

public interface IJwtSettings
{
    string SecretKey { get; }
    string Issuer { get; }
    string Audience { get; }
    TimeSpan Lifetime { get; }
}