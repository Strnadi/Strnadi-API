using Administration.Domain.Entities.Enums;

namespace Administration.Domain.Entities;

public class Document
{
    public Guid Id { get; set; }
    
    public DocumentType Type { get; set; }
    
    public int Version { get; set; }
    
    public string Content { get; set; }
    
    public DateTime PublishedAt { get; set; }
    
    public bool IsActive { get; set; }
    
    public Guid? ProjectId { get; set; }
    
    public virtual Project? Project { get; set; }
}