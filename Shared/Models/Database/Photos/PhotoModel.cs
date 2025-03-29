namespace Shared.Models.Database.Photos;

public class PhotoModel
{
    public int Id { get; set; }

    public string FilePath { get; set; }

    public int RecordingId { get; set; }
    
    public string UserEmail { get; set; }
    
    public string Format { get; set; }
}