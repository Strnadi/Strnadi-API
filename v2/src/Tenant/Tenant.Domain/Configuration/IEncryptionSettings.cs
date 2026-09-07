namespace Tenant.Domain.Configuration;

public interface IEncryptionSettings
{
    string Key { get; }
}