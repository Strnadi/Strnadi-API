using Strnadi.Domain.Entities;
using Strnadi.Domain.Exceptions;
using Strnadi.Domain.Persistence;
using Strnadi.Domain.Persistence.Repositories;
using Strnadi.Domain.Services;

namespace Strnadi.Application.Recordings;

public class RecordingsService(
    IRecordingsRepository recordings,
    IDialectsRepository dialects,
    IFileStorage fileStorage,
    IUnitOfWork unitOfWork)
{
    public async Task<RecordingResponse[]> GetAllAsync(int? userId, bool includeParts, bool includeSound, CancellationToken cancellationToken = default)
    {
        var results = await recordings.GetAllAsync(userId, includeParts, cancellationToken);

        var responses = new List<RecordingResponse>(results.Length);
        foreach (var recording in results)
            responses.Add(await BuildResponseAsync(recording, includeParts, includeSound, cancellationToken));

        return responses.ToArray();
    }

    public async Task<RecordingResponse> GetByIdAsync(int id, bool includeParts, bool includeSound, CancellationToken cancellationToken = default)
    {
        var recording = await recordings.GetByIdAsync(id, includeParts, cancellationToken);
        if (recording is null || recording.Deleted == true)
            throw new NotFoundException(nameof(Recording), id);

        return await BuildResponseAsync(recording, includeParts, includeSound, cancellationToken);
    }

    private async Task<RecordingResponse> BuildResponseAsync(Recording recording, bool includeParts, bool includeSound, CancellationToken cancellationToken)
    {
        RecordingPartResponse[]? parts = null;

        if (includeParts)
        {
            var partResponses = new List<RecordingPartResponse>(recording.RecordingParts.Count);

            foreach (var part in recording.RecordingParts)
            {
                string? audioBase64 = null;
                if (includeSound && part.FilePath is not null)
                {
                    var bytes = await fileStorage.ReadAsync(part.FilePath, cancellationToken);
                    audioBase64 = bytes is not null ? Convert.ToBase64String(bytes) : null;
                }

                partResponses.Add(new RecordingPartResponse(
                    part.Id, part.StartDate, part.EndDate,
                    part.GpsLatitudeStart, part.GpsLongitudeStart,
                    part.GpsLatitudeEnd, part.GpsLongitudeEnd,
                    part.Length, audioBase64));
            }

            parts = partResponses.ToArray();
        }

        return new RecordingResponse(
            recording.Id, recording.CreatedAt, recording.EstimatedBirdsCount, recording.ByApp,
            recording.Name, recording.Note, recording.NotePost, recording.Device,
            recording.UserId, recording.ExpectedPartsCount, parts);
    }

    public async Task DeleteAsync(int id, bool final, int callerId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var recording = await recordings.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Recording), id);

        if (!isAdmin && recording.UserId != callerId)
            throw new ForbiddenException("You are not the owner of this recording");

        if (final && !isAdmin)
            throw new ForbiddenException("Only admins can permanently delete a recording");

        if (final)
            recordings.Remove(recording);
        else
            recording.Deleted = true;

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CreateAsync(RecordingUploadRequest request, int callerId, CancellationToken cancellationToken = default)
    {
        var recording = new Recording
        {
            UserId = callerId,
            CreatedAt = request.CreatedAt,
            EstimatedBirdsCount = request.EstimatedBirdsCount,
            Device = request.Device,
            ByApp = request.ByApp,
            Note = request.Note,
            Name = request.Name,
            ExpectedPartsCount = request.ExpectedPartsCount,
        };

        recordings.Add(recording);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return recording.Id;
    }

    public Task<Recording[]> GetIncompleteAsync(int callerId, CancellationToken cancellationToken = default) =>
        recordings.GetIncompleteAsync(callerId, cancellationToken);

    public async Task UpdateAsync(int id, UpdateRecordingRequest request, int callerId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var recording = await recordings.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Recording), id);

        if (!isAdmin && recording.UserId != callerId)
            throw new ForbiddenException("You are not the owner of this recording");

        recording.Name = request.Name ?? recording.Name;
        if (request.EstimatedBirdsCount is not null)
            recording.EstimatedBirdsCount = (short)request.EstimatedBirdsCount.Value;
        recording.Note = request.Note ?? recording.Note;
        recording.NotePost = request.NotePost ?? recording.NotePost;
        recording.Device = request.Device ?? recording.Device;

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<Recording[]> GetDeletedAsync(CancellationToken cancellationToken = default) =>
        recordings.GetDeletedAsync(cancellationToken);

    public Task<Dialect[]> GetDialectsAsync(CancellationToken cancellationToken = default) =>
        dialects.GetAllAsync(cancellationToken);
}
