namespace Administration.Application.Documents;

public record DocumentAcceptanceResponse(
    Guid Id,
    Guid UserId,
    Guid DocumentId,
    DateTime AcceptedAt,
    DateTime? RevokedAt);

public record CreateDocumentAcceptanceRequest(Guid DocumentId);
