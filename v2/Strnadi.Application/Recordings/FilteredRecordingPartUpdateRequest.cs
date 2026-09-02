using Strnadi.Domain.Enums;

namespace Strnadi.Application.Recordings;

public record FilteredRecordingPartUpdateRequest(
    int? RecordingId,
    int? ParentId,
    DateTime? StartDate,
    DateTime? EndDate,
    FilteredRecordingPartState? State,
    bool? Representant);
