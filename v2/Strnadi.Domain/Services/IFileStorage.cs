namespace Strnadi.Domain.Services;

public interface IFileStorage
{
    Task<string> SaveAsync(string relativePath, byte[] content, CancellationToken cancellationToken = default);
    Task<byte[]?> ReadAsync(string relativePath, CancellationToken cancellationToken = default);
    void Delete(string relativePath, CancellationToken cancellationToken = default);
    bool Exists(string relativePath);
}