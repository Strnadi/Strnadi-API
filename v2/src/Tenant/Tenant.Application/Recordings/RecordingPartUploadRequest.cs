namespace Tenant.Application.Recordings;

// Class with settable properties (not a record) because this is bound both from JSON body and from form data.
public class RecordingPartUploadRequest
{
    public int RecordingId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal GpsLatitudeStart { get; set; }
    public decimal GpsLatitudeEnd { get; set; }
    public decimal GpsLongitudeStart { get; set; }
    public decimal GpsLongitudeEnd { get; set; }
    public string? DataBase64 { get; set; }
}
