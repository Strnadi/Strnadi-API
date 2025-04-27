using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models.Database.Dialects;

public class DetectedDialectModel
{
    public int Id { get; set; }

    public int UserGuessDialectId { get; set; }

    [NotMapped] 
    public string UserGuessDialect { get; set; }

    public int ConfirmedDialectId { get; set; }
    
    [NotMapped]
    public string ConfirmedDialect { get; set; }
    
    public int FilteredRecordingPartId { get; set; }
}