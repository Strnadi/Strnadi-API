using Administration.Domain.Entities.Enums;

namespace Administration.Application.Documents;

public record DocumentResponse(
    Guid Id,
    DocumentType Type,
    int Version,
    string Content,
    DateTime PublishedAt,
    DateTime EffectiveAt,
    bool IsActive,
    Guid? ProjectId);

public record CreateDocumentRequest(DocumentType Type, string Content, Guid? ProjectId, DateTime? EffectiveAt = null);

public record UpdateDocumentRequest(string Content, DateTime? EffectiveAt = null);
