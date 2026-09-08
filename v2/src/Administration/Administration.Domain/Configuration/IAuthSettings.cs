namespace Administration.Domain.Configuration;

public interface IAuthSettings
{
    IGoogleAuthSettings GoogleAuthSettings { get; }
    IAppleAuthSettings AppleAuthSettings { get; }
}
