using Microsoft.Extensions.Configuration;
using Strnadi.Domain.Configuration;

namespace Strnadi.Infrastructure.Configuration;

public class GoogleAuthSettings(IConfiguration configuration) : IGoogleAuthSettings
{
    public string Android => configuration["Auth:Google:Android"]
        ?? throw new InvalidOperationException("Auth:Google:Android is not configured");

    public string Ios => configuration["Auth:Google:Ios"]
        ?? throw new InvalidOperationException("Auth:Google:Ios is not configured");

    public string Web => configuration["Auth:Google:Web"]
        ?? throw new InvalidOperationException("Auth:Google:Web is not configured");

    public string WebSettings => configuration["Auth:Google:WebSecret"]
        ?? throw new InvalidOperationException("Auth:Google:WebSecret is not configured");
}
