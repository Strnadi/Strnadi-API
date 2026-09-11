using Microsoft.Extensions.Logging;
using Platform.Shared.Kernel.Exceptions;
using Platform.Shared.Kernel.Services;
using Tenant.Domain.Entities;
using Tenant.Domain.Persistence;
using Tenant.Domain.Persistence.Repositories;
using Tenant.Domain.Services;

namespace Tenant.Application.Recordings;

public class RecordingPartsService(
    IRecordingPartsRepository recordingParts,
    IFileStorage fileStorage,
    IAudioNormalizer audioNormalizer,
    IClassificationQueue classificationQueue,
    IUnitOfWork unitOfWork,
    ILogger<RecordingPartsService> logger)
{
    public async Task<byte[]> GetSoundAsync(int partId, CancellationToken cancellationToken = default)
    {
        var part = await recordingParts.GetByIdAsync(partId, cancellationToken)
            ?? throw new NotFoundException(nameof(RecordingPart), partId);

        var bytes = part.FilePath is not null
            ? await fileStorage.ReadAsync(part.FilePath, cancellationToken)
            : null;

        return bytes ?? throw new NotFoundException(nameof(RecordingPart), partId);
    }

    public async Task<int> UploadPartAsync(RecordingPartUploadRequest request, CancellationToken cancellationToken = default)
    {
        var part = BuildPart(request);
        recordingParts.Add(part);
        await unitOfWork.SaveChangesAsync(cancellationToken); // need the generated Id for the file path

        if (request.DataBase64 is not null)
            await SaveAudioAsync(part, Convert.FromBase64String(request.DataBase64), cancellationToken);

        return part.Id;
    }

    public async Task<int> UploadPartWithFileAsync(RecordingPartUploadRequest request, byte[] fileContent, CancellationToken cancellationToken = default)
    {
        var part = BuildPart(request);
        recordingParts.Add(part);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await SaveAudioAsync(part, fileContent, cancellationToken);

        await classificationQueue.EnqueueAsync(part.Id, cancellationToken);
        logger.LogInformation("Recording part {PartId} uploaded for recording {RecordingId} and queued for classification", part.Id, part.RecordingId);

        return part.Id;
    }

    private async Task SaveAudioAsync(RecordingPart part, byte[] content, CancellationToken cancellationToken)
    {
        // Keep the original bytes exactly as uploaded (needed later to verify the upload-completion
        // hash, which is computed by the client from the pre-normalization audio) alongside a
        // normalized copy used for playback and AI classification.
        var originalPath = BuildOriginalPath(part.RecordingId!.Value, part.Id);
        await fileStorage.SaveAsync(originalPath, content, cancellationToken);

        var normalizedContent = await audioNormalizer.NormalizeAsync(content, cancellationToken);
        var normalizedPath = BuildNormalizedPath(part.RecordingId!.Value, part.Id);
        await fileStorage.SaveAsync(normalizedPath, normalizedContent, cancellationToken);

        part.FilePath = normalizedPath;
        part.Length = normalizedContent.Length;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static RecordingPart BuildPart(RecordingPartUploadRequest request) => new()
    {
        RecordingId = request.RecordingId,
        StartDate = request.StartDate,
        EndDate = request.EndDate,
        GpsLatitudeStart = request.GpsLatitudeStart,
        GpsLongitudeStart = request.GpsLongitudeStart,
        GpsLatitudeEnd = request.GpsLatitudeEnd,
        GpsLongitudeEnd = request.GpsLongitudeEnd,
    };

    internal static string BuildOriginalPath(int recordingId, int partId) => $"recordings/{recordingId}/{recordingId}_{partId}.original.wav";

    private static string BuildNormalizedPath(int recordingId, int partId) => $"recordings/{recordingId}/{recordingId}_{partId}.normalized.wav";
}
