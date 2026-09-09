namespace Tenant.Domain.Configuration;

public interface IProjectSettings
{
    string Authority { get; }
    Guid ProjectId { get; }
}