namespace Strnadi.Domain.Configuration;

public interface IHostSettings
{
    string ApiHost { get; }
    string WebHost { get; }
}