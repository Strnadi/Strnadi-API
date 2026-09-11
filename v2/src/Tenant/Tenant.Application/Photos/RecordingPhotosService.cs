using Platform.Shared.Kernel.Exceptions;
using Platform.Shared.Kernel.Services;
using Tenant.Domain.Entities;
using Tenant.Domain.Persistence;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Photos;

public class RecordingPhotosService(
    IRecordingPhotosRepository photos,
    IRecordingsRepository recordings,
    IFileStorage fileStorage,
    IUnitOfWork unitOfWork)
{
    public async Task<RecordingPhotoModel[]> GetAllAsync(int recordingId, CancellationToken cancellationToken = default)
    {
        var rows = await photos.GetAllByRecordingIdAsync(recordingId, cancellationToken);

        var results = new List<RecordingPhotoModel>(rows.Length);
        foreach (var photo in rows)
        {
            if (photo.FilePath is null || photo.Format is null)
                continue;

            var content = await fileStorage.ReadAsync(photo.FilePath, cancellationToken);
            if (content is not null)
                results.Add(new RecordingPhotoModel(photo.Id, photo.Format, Convert.ToBase64String(content)));
        }

        return results.ToArray();
    }

    public async Task<int> UploadAsync(int recordingId, UploadRecordingPhotoRequest request, Guid callerId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var recording = await recordings.GetByIdAsync(recordingId, cancellationToken)
            ?? throw new NotFoundException(nameof(Recording), recordingId);

        if (!isAdmin && recording.UserId != callerId)
            throw new ForbiddenException("You are not the owner of this recording");

        var photo = new RecordingPhoto { RecordingId = recordingId, Format = request.Format };
        photos.Add(photo);
        await unitOfWork.SaveChangesAsync(cancellationToken); // need the generated Id for the file path

        photo.FilePath = await fileStorage.SaveAsync(
            $"recordings/{recordingId}/photo_{photo.Id}.{request.Format}",
            Convert.FromBase64String(request.PhotoBase64), cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return photo.Id;
    }

    public async Task DeleteAsync(int recordingId, int photoId, Guid callerId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var recording = await recordings.GetByIdAsync(recordingId, cancellationToken)
            ?? throw new NotFoundException(nameof(Recording), recordingId);

        if (!isAdmin && recording.UserId != callerId)
            throw new ForbiddenException("You are not the owner of this recording");

        var photo = await photos.GetByIdAsync(photoId, cancellationToken);
        if (photo is null || photo.RecordingId != recordingId)
            throw new NotFoundException(nameof(RecordingPhoto), photoId);

        if (photo.FilePath is not null)
            fileStorage.Delete(photo.FilePath, cancellationToken);

        photos.Remove(photo);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
