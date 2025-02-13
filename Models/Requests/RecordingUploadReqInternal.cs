namespace Models.Requests;

public class RecordingUploadReqInternal
{
    public string Email { get; set; }
    
    public DateTime CreatedAt { get; set; }

    public short EstimatedBirdsCount { get; set; }

    public string Device { get; set; }

    public bool ByApp { get; set; }
    
    public string? Note { get; set; }
}