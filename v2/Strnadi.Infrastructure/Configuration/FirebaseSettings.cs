using Microsoft.Extensions.Configuration;
using Strnadi.Domain.Configuration;

namespace Strnadi.Infrastructure.Configuration;

public class FirebaseSettings(IConfiguration configuration) : IFirebaseSettings
{
    public string ServiceAccountJson => configuration["Firebase:ServiceAccountJson"]
        ?? throw new InvalidOperationException("Firebase:ServiceAccountJson is not configured");
}
