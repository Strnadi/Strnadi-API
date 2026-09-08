namespace Administration.Domain.Configuration;

public interface IGoogleAuthSettings
{
    string ClientId { get; }
    string ClientSecret { get; }
}
