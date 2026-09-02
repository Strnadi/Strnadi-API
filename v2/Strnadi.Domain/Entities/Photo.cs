namespace Strnadi.Domain.Entities;

public partial class Photo
{
    public int Id { get; set; }

    public string? FilePath { get; set; }

    public int? RecordingId { get; set; }

    public string? Format { get; set; }

    public int? UserId { get; set; }

    public virtual Recording? Recording { get; set; }

    public virtual User? User { get; set; }
}
