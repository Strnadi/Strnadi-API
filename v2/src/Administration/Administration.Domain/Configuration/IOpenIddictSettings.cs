namespace Administration.Domain.Configuration;

public interface IOpenIddictSettings
{
    string TenantClientSecret { get; }
}