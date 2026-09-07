using Microsoft.Extensions.Configuration;
using Tenant.Domain.Configuration;

namespace Tenant.Infrastructure.Configuration;

public class ClassificationSettings(IConfiguration configuration) : IClassificationSettings
{
    public string BaseUrl => configuration["Classification:BaseUrl"]
        ?? throw new InvalidOperationException("Classification:BaseUrl is not configured");
}