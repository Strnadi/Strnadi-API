namespace Tenant.Application.Photos;

public record RecordingPhotoModel(int Id, string Format, string PhotoBase64);

public record UploadRecordingPhotoRequest(string Format, string PhotoBase64);
