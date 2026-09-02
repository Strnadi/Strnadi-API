using Microsoft.Extensions.Configuration;
using Tenant.Domain.Configuration;

namespace Tenant.Infrastructure.Configuration;

public class FirebaseSettings(IConfiguration configuration) : IFirebaseSettings
{
    public string ServiceAccountJson => configuration["Firebase:ServiceAccountJson"]
        ?? throw new InvalidOperationException("Firebase:ServiceAccountJson is not configured");
}
