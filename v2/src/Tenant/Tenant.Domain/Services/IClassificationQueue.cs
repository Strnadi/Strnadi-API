namespace Tenant.Domain.Services;

public interface IClassificationQueue
{
    Task EnqueueAsync(int recordingPartId, CancellationToken cancellationToken = default);
}