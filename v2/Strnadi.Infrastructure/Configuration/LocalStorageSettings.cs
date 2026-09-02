using Microsoft.Extensions.Configuration;
using Strnadi.Domain.Configuration;

namespace Strnadi.Infrastructure.Configuration;

public class LocalStorageSettings(IConfiguration configuration) : IFileStorageSettings
{
    public string RootPath => configuration["Storage:RootPath"]
        ?? AppContext.BaseDirectory;
}