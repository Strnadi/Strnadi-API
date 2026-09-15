using Administration.Domain.Entities.Enums;

namespace Administration.Application.Documents;

public record DocumentResponse(
    Guid Id,
    DocumentType Type,
    string Title,
    int Version,
    string Content,
    DateTime PublishedAt,
    DateTime EffectiveAt,
    bool IsActive,
    bool IsRequired,
    Guid? ProjectId);

public record CreateDocumentRequest(
    DocumentType Type, string Title, string Content, Guid? ProjectId, DateTime? EffectiveAt = null, bool IsRequired = true);

public record UpdateDocumentRequest(string Content, string? Title = null, DateTime? EffectiveAt = null, bool? IsRequired = null);
