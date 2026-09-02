using Tenant.Domain.Configuration;
using Tenant.Domain.Services;

namespace Tenant.Infrastructure.Storage;

public class LocalStorage(IFileStorageSettings settings) : IFileStorage 
{
    public async Task<string> SaveAsync(string relativePath, byte[] content, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);
        return fullPath;
    }

    public async Task<byte[]?> ReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(relativePath);
        return File.Exists(fullPath) ? await File.ReadAllBytesAsync(fullPath, cancellationToken) : null;
    }

    public void Delete(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(relativePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    public bool Exists(string relativePath)
    {
        return File.Exists(Resolve(relativePath));
    }

    private string Resolve(string relativePath) => Path.Combine(settings.RootPath, relativePath);
}