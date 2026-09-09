namespace Tenant.Domain.Entities;

public partial class Recording
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public short? EstimatedBirdsCount { get; set; }

    public bool ByApp { get; set; }

    public string? Name { get; set; }

    public string? Note { get; set; }

    public string? NotePost { get; set; }

    public string? Device { get; set; }

    public Guid? UserId { get; set; }

    public bool? Deleted { get; set; }

    public bool Legacy { get; set; }

    public int? ExpectedPartsCount { get; set; }

    public bool UploadConfirmed { get; set; }

    public virtual ICollection<FilteredRecordingPart> FilteredRecordingParts { get; set; } = new List<FilteredRecordingPart>();

    public virtual ICollection<RecordingPart> RecordingParts { get; set; } = new List<RecordingPart>();
}
