using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Shared.BackgroundServices.AudioProcessing;

public class AudioProcessingService : BackgroundService
{
    private readonly AudioProcessingQueue _queue;
    private readonly IServiceProvider _services;
    private readonly ILogger<AudioProcessingService> _logger;

    public AudioProcessingService(AudioProcessingQueue queue, IServiceProvider services)
    {
        _queue = queue;
        _services = services;
        _logger = services.GetService<ILogger<AudioProcessingService>>() ?? throw new ArgumentNullException(nameof(services));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await _queue.DequeueAsync(stoppingToken);
            using var scope = _services.CreateScope(); 
            try
            {
                await workItem(scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }
    }}