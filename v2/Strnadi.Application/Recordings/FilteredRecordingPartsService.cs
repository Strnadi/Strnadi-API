using Strnadi.Domain.Entities;
using Strnadi.Domain.Enums;
using Strnadi.Domain.Exceptions;
using Strnadi.Domain.Persistence;
using Strnadi.Domain.Persistence.Repositories;

namespace Strnadi.Application.Recordings;

public class FilteredRecordingPartsService(
    IFilteredRecordingPartsRepository filteredParts,
    IDetectedDialectsRepository detectedDialects,
    IDialectsRepository dialects,
    IUnitOfWork unitOfWork)
{
    public Task<FilteredRecordingPart[]> GetAllAsync(int? recordingId, bool? verified, CancellationToken cancellationToken = default) =>
        filteredParts.GetAllAsync(recordingId, verified, cancellationToken);

    public async Task<FilteredRecordingPart> GetByIdAsync(int fpId, CancellationToken cancellationToken = default) =>
        await filteredParts.GetByIdAsync(fpId, cancellationToken) ?? throw new NotFoundException(nameof(FilteredRecordingPart), fpId);

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
