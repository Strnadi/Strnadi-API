namespace Platform.Shared.Kernel.Services;

public interface IFileStorage
{
    Task<string> SaveAsync(string relativePath, byte[] content, CancellationToken cancellationToken = default);
    Task<byte[]?> ReadAsync(string relativePath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string relativePath);
}
