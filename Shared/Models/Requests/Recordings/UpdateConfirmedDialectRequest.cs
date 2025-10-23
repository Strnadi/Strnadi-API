namespace Shared.Models.Requests.Recordings;

public struct UpdateConfirmedDialectRequest
{
    public int FilteredPartId { get; set; }
    
    public DateTime? StartDate { get; set; }
    
    public DateTime? EndDate { get; set; }
    
    public string? ConfirmedDialectCode { get; set; } 
    
    public bool? Representant { get; set; }
}