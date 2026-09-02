namespace Tenant.Domain.Configuration;

public interface IGoogleAuthSettings
{
    string Android { get; }
    string Ios { get; }
    string Web { get; }
    string WebSettings { get; }
}