namespace Strnadi.Domain.Entities;

public partial class FilteredRecordingPart
{
    public int Id { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string? ProbabilityVector { get; set; }

    public short? State { get; set; }

    public bool? RepresentantFlag { get; set; }

    public int RecordingId { get; set; }

    public virtual DetectedDialect? DetectedDialect { get; set; }

    public virtual Recording Recording { get; set; } = null!;
}
