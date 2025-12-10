using Shared.Models.Database.Recordings;

namespace Shared.Models.Requests.Recordings;

public class FilteredRecordingPartUpdateRequest
{
    public int? RecordingId { get; set; }
    
    public int? ParentId { get; set; }
    
    public DateTime? StartDate { get; set; }
    
    public DateTime? EndDate { get; set; }
    
    public FilteredRecordingPartState? State { get; set; }
    
    public bool? Representant { get; set; }
}