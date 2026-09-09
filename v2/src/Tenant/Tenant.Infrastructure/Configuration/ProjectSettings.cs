using Microsoft.Extensions.Configuration;
using Tenant.Domain.Configuration;

namespace Tenant.Infrastructure.Configuration;

public class ProjectSettings(IConfiguration configuration) : IProjectSettings
{
    public string Authority => configuration["Administration:Authority"]
        ?? throw new InvalidOperationException("Administration:Authority is not configured");

    public Guid ProjectId => Guid.Parse(configuration["Administration:ProjectId"] 
        ?? throw new InvalidOperationException("Administration:ProjectId is not configured"));
}