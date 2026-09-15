namespace Administration.Domain.Entities;

public class DocumentAcceptance
{
    public Guid Id { get; set; }
    
    public Guid UserId { get; set; }

    public virtual User User { get; set; }

    public Guid DocumentId { get; set; }
    
    public DateTime AcceptedAt { get; set; }
    
    public string? IpAddress { get; set; }
    
    public virtual Document Document { get; set; }
}