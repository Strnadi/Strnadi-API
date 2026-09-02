namespace Strnadi.Application.Recordings;

public record FilteredRecordingPartUploadRequest(int RecordingId, DateTime StartDate, DateTime EndDate, string DialectCode);
