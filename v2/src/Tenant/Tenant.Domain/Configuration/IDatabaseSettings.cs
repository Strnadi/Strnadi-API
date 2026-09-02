namespace Tenant.Domain.Configuration;

public interface IDatabaseSettings
{
    string ConnectionString { get; }
}