using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Platform.Shared.Kernel.Exceptions;
using Platform.Shared.Kernel.Services;
using Tenant.Domain.Entities;
using Tenant.Domain.Persistence;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Recordings;

public class RecordingsService(
    IRecordingsRepository recordings,
    IDialectsRepository dialects,
    IFileStorage fileStorage,
    IUnitOfWork unitOfWork,
    ILogger<RecordingsService> logger)
{
    public async Task<RecordingResponse[]> GetAllAsync(Guid? userId, bool includeParts, CancellationToken cancellationToken = default)
    {
        var results = await recordings.GetAllAsync(userId, includeParts, cancellationToken);
        return results.Select(recording => BuildResponse(recording, includeParts)).ToArray();
    }

    public async Task<RecordingResponse> GetByIdAsync(int id, bool includeParts, CancellationToken cancellationToken = default)
    {
        var recording = await recordings.GetByIdAsync(id, includeParts, cancellationToken);
        if (recording is null || recording.Deleted == true)
            throw new NotFoundException(nameof(Recording), id);

        return BuildResponse(recording, includeParts);
    }

    // Audio itself is never embedded here - the only way to get a part's audio is the dedicated
    // GET /recordings/part/{partId}/sound endpoint (RecordingPartsService.GetSoundAsync), which
    // owns the ownership/DownloadRecordings check. This used to also support ?sound=true, base64-
    // embedding the audio inline - removed as obsolete.
    private static RecordingResponse BuildResponse(Recording recording, bool includeParts)
    {
        var parts = includeParts
            ? recording.RecordingParts.Select(part => new RecordingPartResponse(
                part.Id, part.StartDate, part.EndDate,
                part.GpsLatitudeStart, part.GpsLongitudeStart,
                part.GpsLatitudeEnd, part.GpsLongitudeEnd,
                part.Length)).ToArray()
            : null;

        return new RecordingResponse(
            recording.Id, recording.CreatedAt, recording.EstimatedBirdsCount, recording.ByApp,
            recording.Name, recording.Note, recording.NotePost, recording.Device,
            recording.UserId, recording.ExpectedPartsCount, recording.UploadConfirmed, parts);
    }

    public async Task DeleteAsync(int id, bool final, Guid callerId, bool canModerateRecordings, bool canDeleteRecordings, CancellationToken cancellationToken = default)
    {
        var recording = await recordings.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Recording), id);

        if (!canModerateRecordings && recording.UserId != callerId)
            throw new ForbiddenException("You are not the owner of this recording");

        if (final && !canDeleteRecordings)
            throw new ForbiddenException("You are not allowed to permanently delete a recording");

        if (final)
            recordings.Remove(recording);
        else
            recording.Deleted = true;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Recording {RecordingId} {Action} by {CallerId}", id, final ? "permanently deleted" : "soft-deleted", callerId);
    }

    public async Task<int> CreateAsync(RecordingUploadRequest request, Guid callerId, CancellationToken cancellationToken = default)
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

        logger.LogInformation("Recording {RecordingId} created by user {CallerId} ({ExpectedParts} expected parts)",
            recording.Id, callerId, recording.ExpectedPartsCount);

        return recording.Id;
    }

    public Task<Recording[]> GetIncompleteAsync(Guid callerId, CancellationToken cancellationToken = default) =>
        recordings.GetIncompleteAsync(callerId, cancellationToken);

    public async Task UpdateAsync(int id, UpdateRecordingRequest request, Guid callerId, bool canModerateRecordings, CancellationToken cancellationToken = default)
    {
        var recording = await recordings.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Recording), id);

        if (!canModerateRecordings && recording.UserId != callerId)
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

    // The mobile app computes the same hash client-side (before this server normalizes any audio)
    // and sends it once it believes it has uploaded every part; a match is the client's proof that
    // every part arrived intact, replacing the old delayed check-and-notify job.
    public async Task<bool> CompleteUploadAsync(int id, string hash, Guid callerId, CancellationToken cancellationToken = default)
    {
        var recording = await recordings.GetByIdAsync(id, includeParts: true, cancellationToken)
            ?? throw new NotFoundException(nameof(Recording), id);

        if (recording.UserId != callerId)
            throw new ForbiddenException("You are not the owner of this recording");

        var content = new StringBuilder();
        foreach (var part in recording.RecordingParts.OrderBy(p => p.StartDate))
        {
            var originalPath = RecordingPartsService.BuildOriginalPath(id, part.Id);
            var original = await fileStorage.ReadAsync(originalPath, cancellationToken);
            if (original is null)
            {
                logger.LogWarning("Upload completion for recording {RecordingId} failed: part {PartId} has no stored audio", id, part.Id);
                return false;
            }

            content.Append(part.StartDate?.ToString("O"));
            content.Append(part.EndDate?.ToString("O"));
            content.Append(part.GpsLatitudeStart?.ToString(CultureInfo.InvariantCulture));
            content.Append(part.GpsLongitudeStart?.ToString(CultureInfo.InvariantCulture));
            content.Append(part.GpsLatitudeEnd?.ToString(CultureInfo.InvariantCulture));
            content.Append(part.GpsLongitudeEnd?.ToString(CultureInfo.InvariantCulture));
            content.Append(Convert.ToBase64String(original));
        }

        var computedHash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(content.ToString())));
        if (!string.Equals(computedHash, hash, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Upload completion for recording {RecordingId} failed: hash mismatch", id);
            return false;
        }

        recording.UploadConfirmed = true;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Recording {RecordingId} upload confirmed", id);
        return true;
    }
}
