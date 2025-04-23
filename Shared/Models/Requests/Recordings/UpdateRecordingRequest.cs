using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models.Requests.Recordings;

public class UpdateRecordingRequest
{
    [Column("name")]
    public string? Name { get; set; }

    [Column("estimated_birds_count")]
    public int? EstimatedBirdsCount { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("note_post")]
    public string? NotePost { get; set; }

    [Column("device")]
    public string? Device { get; set; }
}