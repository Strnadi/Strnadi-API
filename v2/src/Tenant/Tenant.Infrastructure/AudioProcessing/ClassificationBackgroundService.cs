using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tenant.Application.Recordings;

namespace Tenant.Infrastructure.AudioProcessing;

public class ClassificationBackgroundService(
    ClassificationQueue queue,
    IServiceProvider services,
    ILogger<ClassificationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var recordingPartId = await queue.DequeueAsync(stoppingToken);

            using var scope = services.CreateScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<DialectClassificationService>()
                    .ClassifyPartAsync(recordingPartId, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to classify recording part {RecordingPartId}", recordingPartId);
            }
        }
    }
}