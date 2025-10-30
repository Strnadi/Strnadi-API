using Microsoft.Extensions.Logging;
using Quartz;
using Repository;
using Shared.Tools;

namespace Recordings.Jobs;

public class CheckRecordingJob : IJob
{
    private readonly ILogger<CheckRecordingJob> _logger;
    private readonly RecordingsRepository _repository;
    private readonly FirebaseNotificationService _notifications;

    public CheckRecordingJob(ILogger<CheckRecordingJob> logger, RecordingsRepository repository, FirebaseNotificationService notifications)
    {
        _logger = logger;
        _repository = repository;
        _notifications = notifications;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var recordingId = context.JobDetail.JobDataMap.GetInt("recordingId");

        var recording = await _repository.GetByIdAsync(recordingId, true, false);
        if (recording is null)
        {
            _logger.LogWarning($"Recording {recordingId} not found");
            return;
        }

        if (recording.ExpectedPartsCount == recording.Parts!.Count())
        {
            _logger.LogWarning($"Recording {recordingId} incomplete ({recording.Parts!.Count()}/{recording.ExpectedPartsCount})");
            
            var fcmToken = context.JobDetail.JobDataMap.GetString("fcmToken");
            await _notifications.SendNotificationAsync(fcmToken, "Blyat", "Suka");
        }
    }
}