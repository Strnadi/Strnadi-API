using Platform.Shared.Kernel.Exceptions;
using Tenant.Domain.Entities;
using Tenant.Domain.Enums;
using Tenant.Domain.Persistence;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Recordings;

public class FilteredRecordingPartsService(
    IFilteredRecordingPartsRepository filteredParts,
    IDetectedDialectsRepository detectedDialects,
    IDialectsRepository dialects,
    IUnitOfWork unitOfWork)
{
    public async Task<FilteredRecordingPartResponse[]> GetAllAsync(int? recordingId, bool? verified, CancellationToken cancellationToken = default)
    {
        var parts = await filteredParts.GetAllAsync(recordingId, verified, cancellationToken);
        var predictedDialectCodes = await GetPredictedDialectCodesAsync(parts, cancellationToken);
        return parts.Select(p => BuildResponse(p, predictedDialectCodes)).ToArray();
    }

    public async Task<FilteredRecordingPartResponse> GetByIdAsync(int fpId, CancellationToken cancellationToken = default)
    {
        var part = await filteredParts.GetByIdAsync(fpId, cancellationToken)
            ?? throw new NotFoundException(nameof(FilteredRecordingPart), fpId);

        var predictedDialectCodes = await GetPredictedDialectCodesAsync([part], cancellationToken);
        return BuildResponse(part, predictedDialectCodes);
    }

    // PredictedDialectId has no navigation property in the schema (only ConfirmedDialect/UserGuessDialect
    // do), so resolving its code needs a manual lookup instead of an EF Include.
    private async Task<Dictionary<int, string>> GetPredictedDialectCodesAsync(
        IEnumerable<FilteredRecordingPart> parts, CancellationToken cancellationToken)
    {
        var predictedIds = parts
            .Select(p => p.DetectedDialect?.PredictedDialectId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToHashSet();

        if (predictedIds.Count == 0)
            return [];

        var allDialects = await dialects.GetAllAsync(cancellationToken);
        return allDialects.Where(d => predictedIds.Contains(d.Id)).ToDictionary(d => d.Id, d => d.DialectCode);
    }

    private static FilteredRecordingPartResponse BuildResponse(FilteredRecordingPart part, Dictionary<int, string> predictedDialectCodes)
    {
        var recording = new RecordingResponse(
            part.Recording.Id, part.Recording.CreatedAt, part.Recording.EstimatedBirdsCount, part.Recording.ByApp,
            part.Recording.Name, part.Recording.Note, part.Recording.NotePost, part.Recording.Device,
            part.Recording.UserId, part.Recording.ExpectedPartsCount, part.Recording.UploadConfirmed, null);

        var detected = Array.Empty<DetectedDialectResponse>();
        if (part.DetectedDialect is not null)
        {
            var dd = part.DetectedDialect;
            var predictedDialect = dd.PredictedDialectId is not null
                && predictedDialectCodes.TryGetValue(dd.PredictedDialectId.Value, out var code)
                    ? code
                    : null;

            detected = [new DetectedDialectResponse(
                dd.Id, dd.UserGuessDialect?.DialectCode, dd.ConfirmedDialect?.DialectCode, predictedDialect, dd.FilteredRecordingPartId)];
        }

        return new FilteredRecordingPartResponse(
            part.Id, part.StartDate, part.EndDate, part.State, part.RepresentantFlag, part.RecordingId, recording, detected);
    }

    public async Task<int> UploadAsync(FilteredRecordingPartUploadRequest request, CancellationToken cancellationToken = default)
    {
        var dialect = await dialects.GetByCodeAsync(request.DialectCode, cancellationToken)
            ?? throw new NotFoundException(nameof(Dialect), request.DialectCode);

        var filteredPart = new FilteredRecordingPart
        {
            RecordingId = request.RecordingId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            State = (short)FilteredRecordingPartState.AwaitingProcession,
        };

        filteredParts.Add(filteredPart);
        await unitOfWork.SaveChangesAsync(cancellationToken); // need the generated Id

        detectedDialects.Add(new DetectedDialect
        {
            FilteredRecordingPartId = filteredPart.Id,
            UserGuessDialectId = dialect.Id,
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return filteredPart.Id;
    }

    public async Task<int> PostConfirmedDialectAsync(PostConfirmedDialectRequest request, CancellationToken cancellationToken = default)
    {
        var dialect = await dialects.GetByCodeAsync(request.DialectCode, cancellationToken)
            ?? throw new NotFoundException(nameof(Dialect), request.DialectCode);

        var filteredPart = new FilteredRecordingPart
        {
            RecordingId = request.RecordingId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            RepresentantFlag = request.Representant,
            State = (short)FilteredRecordingPartState.ConfirmedManually,
        };

        filteredParts.Add(filteredPart);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        detectedDialects.Add(new DetectedDialect
        {
            FilteredRecordingPartId = filteredPart.Id,
            ConfirmedDialectId = dialect.Id,
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return filteredPart.Id;
    }

    public async Task UpdateConfirmedDialectAsync(UpdateConfirmedDialectRequest request, CancellationToken cancellationToken = default)
    {
        var filteredPart = await filteredParts.GetByIdAsync(request.FilteredPartId, cancellationToken)
            ?? throw new NotFoundException(nameof(FilteredRecordingPart), request.FilteredPartId);

        filteredPart.StartDate = request.StartDate ?? filteredPart.StartDate;
        filteredPart.EndDate = request.EndDate ?? filteredPart.EndDate;
        filteredPart.RepresentantFlag = request.Representant ?? filteredPart.RepresentantFlag;

        if (request.ConfirmedDialectCode is not null)
        {
            var dialect = await dialects.GetByCodeAsync(request.ConfirmedDialectCode, cancellationToken)
                ?? throw new NotFoundException(nameof(Dialect), request.ConfirmedDialectCode);

            var detectedDialect = await detectedDialects.GetByFilteredPartIdAsync(filteredPart.Id, cancellationToken);
            if (detectedDialect is null)
                detectedDialects.Add(new DetectedDialect { FilteredRecordingPartId = filteredPart.Id, ConfirmedDialectId = dialect.Id });
            else
                detectedDialect.ConfirmedDialectId = dialect.Id;

            // RecordingPointResolver (map rendering) only recognizes an admin decision via State,
            // not via ConfirmedDialectId alone - without this the map keeps showing the AI/user
            // source and color even after this confirmation.
            filteredPart.State = (short)(detectedDialect?.PredictedDialectId is not null
                ? FilteredRecordingPartState.DetectedByAiAndConfirmed
                : FilteredRecordingPartState.ConfirmedManually);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(int fpId, FilteredRecordingPartUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var filteredPart = await filteredParts.GetByIdAsync(fpId, cancellationToken)
            ?? throw new NotFoundException(nameof(FilteredRecordingPart), fpId);

        filteredPart.RecordingId = request.RecordingId ?? filteredPart.RecordingId;
        filteredPart.StartDate = request.StartDate ?? filteredPart.StartDate;
        filteredPart.EndDate = request.EndDate ?? filteredPart.EndDate;
        filteredPart.RepresentantFlag = request.Representant ?? filteredPart.RepresentantFlag;
        if (request.State is not null)
            filteredPart.State = (short)request.State.Value;

        // request.ParentId has no backing column in the current schema (filtered_recording_parts has
        // no parent_id) - intentionally ignored.

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteConfirmedDialectAsync(int filteredPartId, CancellationToken cancellationToken = default) =>
        DeleteAsync(filteredPartId, cancellationToken);

    public async Task DeleteAsync(int fpId, CancellationToken cancellationToken = default)
    {
        var filteredPart = await filteredParts.GetByIdAsync(fpId, cancellationToken)
            ?? throw new NotFoundException(nameof(FilteredRecordingPart), fpId);

        filteredParts.Remove(filteredPart);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
