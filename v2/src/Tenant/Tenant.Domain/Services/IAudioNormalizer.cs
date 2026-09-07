namespace Tenant.Domain.Services;

public interface IAudioNormalizer
{
    Task<byte[]> NormalizeAsync(byte[] input, CancellationToken cancellationToken = default);
}