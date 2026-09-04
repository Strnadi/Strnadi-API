namespace Tenant.Domain.Entities;

using Microsoft.EntityFrameworkCore;

[Index(nameof(GpsLatitudeEnd), nameof(GpsLongitudeEnd))]
[Index(
    nameof(RecordingId),
    nameof(EndDate),
    nameof(Id),
    IsDescending = new[] { false, true, true })]
public partial class RecordingPart
{
    public int Id { get; set; }

    public int? RecordingId { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public decimal? GpsLatitudeStart { get; set; }

    public decimal? GpsLongitudeStart { get; set; }

    public decimal? GpsLatitudeEnd { get; set; }

    public decimal? GpsLongitudeEnd { get; set; }

    public string? FilePath { get; set; }

    public int? Length { get; set; }

    public virtual Recording? Recording { get; set; }
}
