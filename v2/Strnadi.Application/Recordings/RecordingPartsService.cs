using Strnadi.Domain.Entities;
using Strnadi.Domain.Exceptions;
using Strnadi.Domain.Persistence;
using Strnadi.Domain.Persistence.Repositories;
using Strnadi.Domain.Services;

namespace Strnadi.Application.Recordings;

public class RecordingPartsService(IRecordingPartsRepository recordingParts, IFileStorage fileStorage, IUnitOfWork unitOfWork)
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

        // NOTE: old code enqueued this part for AI dialect classification here. Deferred on purpose -
        // the audio processing pipeline (FFmpeg/AI model/queue) is a separate, later step.
        // TODO

        return part.Id;
    }

    private async Task SaveAudioAsync(RecordingPart part, byte[] content, CancellationToken cancellationToken)
    {
        var path = BuildPath(part.RecordingId!.Value, part.Id);
        await fileStorage.SaveAsync(path, content, cancellationToken);

        part.FilePath = path;
        part.Length = content.Length;
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

    private static string BuildPath(int recordingId, int partId) => $"recordings/{recordingId}/{recordingId}_{partId}.original.wav";
}
