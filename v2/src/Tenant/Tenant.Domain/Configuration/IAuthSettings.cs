namespace Tenant.Domain.Configuration;

public interface IAuthSettings
{
    IAppleAuthSettings AppleAuthSettings { get; }
    IGoogleAuthSettings GoogleAuthSettings { get; }
}