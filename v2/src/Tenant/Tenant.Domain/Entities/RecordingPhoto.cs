namespace Tenant.Domain.Entities;

public class RecordingPhoto
{
    public int Id { get; set; }

    public int RecordingId { get; set; }

    public string? FilePath { get; set; }

    public string? Format { get; set; }

    public virtual Recording Recording { get; set; } = null!;
}
