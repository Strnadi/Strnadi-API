namespace Shared.Models.Requests.Recordings;

public class DetectedDialectUploadRequest
{
    public int FilteredPartId { get; set; }
    
    public int? UserGuessDialectId { get; set; }
    
    public int? ConfirmedDialectId { get; set; }
    
    public int? PredictedDialectId { get; set; }
}