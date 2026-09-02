using Microsoft.Extensions.Configuration;
using Strnadi.Domain.Configuration;

namespace Strnadi.Infrastructure.Configuration;

public class HostSettings(IConfiguration configuration) : IHostSettings
{
    public string ApiHost => configuration["Host"] 
        ?? throw new InvalidOperationException("Host is not configured");

    public string WebHost => configuration["Host"] 
        ?? throw new InvalidOperationException("Host is not configured");
}