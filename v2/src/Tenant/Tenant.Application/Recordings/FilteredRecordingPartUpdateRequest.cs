using Tenant.Domain.Enums;

namespace Tenant.Application.Recordings;

public record FilteredRecordingPartUpdateRequest(
    int? RecordingId,
    int? ParentId,
    DateTime? StartDate,
    DateTime? EndDate,
    FilteredRecordingPartState? State,
    bool? Representant);
