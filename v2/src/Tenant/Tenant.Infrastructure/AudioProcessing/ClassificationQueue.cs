using System.Threading.Channels;
using Tenant.Domain.Services;

namespace Tenant.Infrastructure.AudioProcessing;

public class ClassificationQueue : IClassificationQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public async Task EnqueueAsync(int recordingPartId, CancellationToken cancellationToken = default) =>
        await _channel.Writer.WriteAsync(recordingPartId, cancellationToken);

    public async Task<int> DequeueAsync(CancellationToken cancellationToken) =>
        await _channel.Reader.ReadAsync(cancellationToken);
}