namespace Shared.Models.Requests;

public class RecordingUploadModel
{
    public DateTime CreatedAt { get; set; }

    public short EstimatedBirdsCount { get; set; }

    public string Device { get; set; }

    public bool ByApp { get; set; }
    
    public string? Note { get; set; }
    
    public string? Name { get; set; }
}