using Microsoft.Extensions.Configuration;
using Platform.Shared.Kernel.Configuration;

namespace Platform.Shared.Infrastructure.Configuration;

public class LocalStorageSettings(IConfiguration configuration) : IFileStorageSettings
{
    public string RootPath => configuration["Storage:RootPath"]
        ?? AppContext.BaseDirectory;
}
