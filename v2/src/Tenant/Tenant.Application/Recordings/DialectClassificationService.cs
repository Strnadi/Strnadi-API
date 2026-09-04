using Tenant.Domain.Entities;
using Tenant.Domain.Enums;
using Tenant.Domain.Persistence;
using Tenant.Domain.Persistence.Repositories;
using Tenant.Domain.Services;

namespace Tenant.Application.Recordings;

// Combines what used to be split across RecordingsController.ClassifyAudioAsync and
// RecordingsRepository.ProcessPredictionAsync in the old backend.
public class DialectClassificationService(
    IRecordingPartsRepository recordingParts,
    IFilteredRecordingPartsRepository filteredParts,
    IDetectedDialectsRepository detectedDialects,
    IDialectsRepository dialects,
    IFileStorage fileStorage,
    IDialectClassifier classifier,
    IUnitOfWork unitOfWork)
{
    public async Task ClassifyPartAsync(int recordingPartId, CancellationToken cancellationToken = default)
    {
        var part = await recordingParts.GetByIdAsync(recordingPartId, cancellationToken);
        if (part?.FilePath is null || part.RecordingId is null || part.StartDate is null)
            return;

        var partStartDate = part.StartDate.Value;

        var audio = await fileStorage.ReadAsync(part.FilePath, cancellationToken);
        if (audio is null)
            return;

        var predictions = await classifier.ClassifyAsync(audio, part.FilePath, cancellationToken);
        if (predictions is null || predictions.Length == 0)
            return;

        foreach (var prediction in predictions)
        {
            var startDate = partStartDate + TimeSpan.FromSeconds(prediction.StartSeconds);
            var endDate = partStartDate + TimeSpan.FromSeconds(prediction.EndSeconds);

            var filteredPart = new FilteredRecordingPart
            {
                RecordingId = part.RecordingId.Value,
                StartDate = startDate,
                EndDate = endDate,
                State = (short)FilteredRecordingPartState.DetectedByAi,
                RepresentantFlag = prediction.IsRepresentant,
            };

            filteredParts.Add(filteredPart);
            await unitOfWork.SaveChangesAsync(cancellationToken); // need the generated Id

            if (prediction.DialectCode is not null)
            {
                var dialect = await dialects.GetByCodeAsync(prediction.DialectCode, cancellationToken);
                if (dialect is not null)
                {
                    detectedDialects.Add(new DetectedDialect
                    {
                        FilteredRecordingPartId = filteredPart.Id,
                        PredictedDialectId = dialect.Id,
                    });
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }
}