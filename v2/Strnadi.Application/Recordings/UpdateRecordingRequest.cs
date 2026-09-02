namespace Strnadi.Application.Recordings;

public record UpdateRecordingRequest(
    string? Name,
    int? EstimatedBirdsCount,
    string? Note,
    string? NotePost,
    string? Device);
