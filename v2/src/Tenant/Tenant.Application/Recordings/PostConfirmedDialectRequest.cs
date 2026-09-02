namespace Tenant.Application.Recordings;

public record PostConfirmedDialectRequest(int RecordingId, DateTime StartDate, DateTime EndDate, string DialectCode, bool Representant);
