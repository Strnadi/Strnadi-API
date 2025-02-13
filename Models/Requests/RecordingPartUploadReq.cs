namespace Models.Requests;

public class RecordingPartUploadReq
{
    public int RecordingId { get; set; }
    
    public DateTime Start { get; set; }
    
    public DateTime End { get; set; }
}