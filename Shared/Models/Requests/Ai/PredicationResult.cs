namespace Shared.Models.Requests.Ai;

public class PredicationResult
{
    public class Segment
    {
        public double[] Interval { get; set; }
        
        public string? Label { get; set; }
        
        public Dictionary<string, double> FullPrediction { get; set; }
    }
    
    public string? Representant { get; set; }
    
    public float Confidence { get; set; }
    
    public Segment[] Segments { get; set; }
}