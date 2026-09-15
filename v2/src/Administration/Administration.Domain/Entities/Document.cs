using Administration.Domain.Entities.Enums;

namespace Administration.Domain.Entities;

public class Document
{
    public Guid Id { get; set; }
    
    public DocumentType Type { get; set; }

    public string Title { get; set; }

    public int Version { get; set; }

    public string Content { get; set; }
    
    public DateTime PublishedAt { get; set; }

    public DateTime EffectiveAt { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Whether accepting this document is mandatory (blocks registration/roles) or optional (e.g. marketing consent).</summary>
    public bool IsRequired { get; set; } = true;

    public Guid? ProjectId { get; set; }
    
    public virtual Project? Project { get; set; }
}