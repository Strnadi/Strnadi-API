using System.Threading.Channels;

namespace Shared.BackgroundServices.AudioProcessing;

public class AudioProcessingQueue
{
    private readonly Channel<Func<IServiceProvider, Task>> _channel = Channel.CreateUnbounded<Func<IServiceProvider, Task>>();

    public async Task Enqueue(Func<IServiceProvider, Task> workItem)
    {
        await _channel.Writer.WriteAsync(workItem);
    }

    public async Task<Func<IServiceProvider, Task>> DequeueAsync(CancellationToken ct) =>
        await _channel.Reader.ReadAsync(ct);
}