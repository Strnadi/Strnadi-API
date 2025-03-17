namespace Shared.Models.Requests.Photos;

public class UploadRecordingPhotoRequest
{
    public int RecordingId { get; set; }
    
    public string PhotosBase64 { get; set; }
    
    public string Format { get; set; }
}