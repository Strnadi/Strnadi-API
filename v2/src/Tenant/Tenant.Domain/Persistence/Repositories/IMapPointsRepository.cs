namespace Tenant.Domain.Persistence.Repositories;

public interface IMapPointsRepository
{
    // Coarse pre-filter only: returns non-deleted recordings that have at least one GPS-bearing
    // part or representative-linked part inside bounds, with filters applied. Application still
    // has to resolve each recording's exact point (RecordingPointResolver) and drop it if that
    // point ends up outside bounds - the representative-to-part GPS join can't be expressed as a
    // single flat SQL predicate.
    Task<RecordingCandidate[]> GetCandidatesInBoundsAsync(MapBounds bounds, MapRecordingFilters filters, CancellationToken cancellationToken = default);

    // Re-checks that a set of recordings are still non-deleted and, if userId is given, still
    // visible under ownership rules - used by cluster-detail pagination to detect membership
    // changes since the overview snapshot was taken.
    Task<int[]> GetVisibleRecordingIdsAsync(int[] recordingIds, CancellationToken cancellationToken = default);
}
