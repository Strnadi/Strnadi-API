namespace Shared.Models.Requests.Recordings;

public class FilteredRecordingPartUploadRequest
{
    public int RecordingPartId { get; set; }
    
    public DateTime StartTime { get; set; }
    
    public DateTime EndTime { get; set; }
}