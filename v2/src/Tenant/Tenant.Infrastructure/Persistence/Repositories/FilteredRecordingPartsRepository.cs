using Microsoft.EntityFrameworkCore;
using Tenant.Domain.Entities;
using Tenant.Domain.Enums;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Infrastructure.Persistence.Repositories;

public class FilteredRecordingPartsRepository(TenantDbContext db) : IFilteredRecordingPartsRepository
{
    // verified = anything except AwaitingProcession/UnableToConfirm.
    private static readonly short[] VerifiedStates =
    [
        (short)FilteredRecordingPartState.ConfirmedWithCorrectGuess,
        (short)FilteredRecordingPartState.ConfirmedWithWrongGuess,
        (short)FilteredRecordingPartState.ConfirmedManually,
        (short)FilteredRecordingPartState.DetectedByAi,
        (short)FilteredRecordingPartState.DetectedByAiAndConfirmed,
    ];

    public Task<FilteredRecordingPart[]> GetAllAsync(int? recordingId, bool? verified, CancellationToken cancellationToken = default)
    {
        var query = IncludeForResponse(db.FilteredRecordingParts);

        if (recordingId is not null)
            query = query.Where(p => p.RecordingId == recordingId);

        if (verified == true)
            query = query.Where(p => p.State != null && VerifiedStates.Contains(p.State.Value));

        return query.ToArrayAsync(cancellationToken);
    }

    public Task<FilteredRecordingPart?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        IncludeForResponse(db.FilteredRecordingParts).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    private static IQueryable<FilteredRecordingPart> IncludeForResponse(IQueryable<FilteredRecordingPart> query) =>
        query
            .Include(p => p.Recording)
            .Include(p => p.DetectedDialect).ThenInclude(d => d!.ConfirmedDialect)
            .Include(p => p.DetectedDialect).ThenInclude(d => d!.UserGuessDialect);

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) =>
        db.FilteredRecordingParts.AnyAsync(p => p.Id == id, cancellationToken);

    public void Add(FilteredRecordingPart filteredPart) => db.FilteredRecordingParts.Add(filteredPart);

    public void Remove(FilteredRecordingPart filteredPart) => db.FilteredRecordingParts.Remove(filteredPart);
}
