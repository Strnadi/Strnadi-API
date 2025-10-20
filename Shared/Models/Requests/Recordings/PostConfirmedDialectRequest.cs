namespace Shared.Models.Requests.Recordings;

public struct PostConfirmedDialectRequest
{
    public int RecordingId { get; set; }
    
    public DateTime StartDate { get; set; }
    
    public DateTime EndDate { get; set; }
    
    public string DialectCode { get; set; }
    
    public bool Representant { get; set; }
}