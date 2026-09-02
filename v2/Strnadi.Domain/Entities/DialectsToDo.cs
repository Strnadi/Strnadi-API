namespace Strnadi.Domain.Entities;

public partial class DialectsToDo
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public int? RecordingId { get; set; }

    public int? RecordingPartsId { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public decimal? GpsLatitudeStart { get; set; }

    public decimal? GpsLongitudeStart { get; set; }

    public string? FilePath { get; set; }

    public int? FilteredRecordingPartId { get; set; }

    public string? ProbabilityVector { get; set; }

    public DateTime? FilteredStartDate { get; set; }

    public DateTime? FilteredEndDate { get; set; }

    public string? GuessDialect { get; set; }

    public string? ConfirmedDialect { get; set; }
}
