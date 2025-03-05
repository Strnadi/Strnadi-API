namespace Models.Database;

public class FilteredSubrecordingModel
{
    public int Id { get; set; }
    
    public int RecordingsId { get; set; }

    public int? BirdsId { get; set; }
    
    public string PathFile { get; set; }

    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    public string ProbabilityVector { get; set; }

    public short State { get; set; }

    public bool RepresentantFlag { get; set; }
}