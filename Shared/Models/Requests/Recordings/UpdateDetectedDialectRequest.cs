namespace Shared.Models.Requests.Recordings;

public class UpdateDetectedDialectRequest
{
    public int Id { get; set; }
    
    public int? UserGuessDialectId { get; set; }
    
    public int? ConfirmedDialectId { get; set; }
    
    public int? PredictedDialectId { get; set; }
}