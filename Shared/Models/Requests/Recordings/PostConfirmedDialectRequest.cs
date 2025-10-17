namespace Shared.Models.Requests.Recordings;

public struct PostConfirmedDialectRequest
{
    public int RecordingId { get; set; }
    
    public DateTime Start { get; set; }
    
    public DateTime End { get; set; }
    
    public string DialectCode { get; set; }
    
    public bool Representant { get; set; }
}