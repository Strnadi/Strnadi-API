namespace Strnadi.Domain.Configuration;

public interface IAppleAuthSettings
{
    string TeamId { get; }
    string KeyId { get; }
    string ClientIdWeb { get; }
    string ClientIdIos { get; }
    string RedirectUriWeb { get; }
    string P8PrivateKey { get; }
}