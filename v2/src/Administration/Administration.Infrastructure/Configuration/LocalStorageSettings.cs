using Administration.Domain.Configuration;
using Microsoft.Extensions.Configuration;

namespace Administration.Infrastructure.Configuration;

public class LocalStorageSettings(IConfiguration configuration) : IFileStorageSettings
{
    public string RootPath => configuration["Storage:RootPath"]
        ?? AppContext.BaseDirectory;
}
