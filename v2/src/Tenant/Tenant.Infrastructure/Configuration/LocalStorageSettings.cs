using Microsoft.Extensions.Configuration;
using Tenant.Domain.Configuration;

namespace Tenant.Infrastructure.Configuration;

public class LocalStorageSettings(IConfiguration configuration) : IFileStorageSettings
{
    public string RootPath => configuration["Storage:RootPath"]
        ?? AppContext.BaseDirectory;
}