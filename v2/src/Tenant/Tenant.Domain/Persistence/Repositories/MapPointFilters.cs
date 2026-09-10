namespace Tenant.Domain.Persistence.Repositories;

public enum OwnerScope { All, Mine, Others }

// Row-filtering concerns only (who/when). Dialect-tier selection and the meaningful-dialect
// filters are display/aggregation concerns decided later in Application (RecordingPointResolver),
// since they depend on DialectMode and on resolving each recording's winning dialect first.
public record MapRecordingFilters(
    OwnerScope OwnerScope,
    Guid? UserId,
    DateOnly? CreatedFrom,
    DateOnly? CreatedTo);
