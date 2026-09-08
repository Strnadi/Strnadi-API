namespace Administration.Domain.Configuration;

public interface IAppleAuthSettings
{
    string ClientId { get; }
    string TeamId { get; }
    string KeyId { get; }
    string P8PrivateKey { get; }
}
