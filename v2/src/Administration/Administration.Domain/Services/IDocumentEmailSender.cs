using Administration.Domain.Entities;

namespace Administration.Domain.Services;

public interface IDocumentEmailSender
{
    /// <summary>Notifies a user that a document they previously accepted has a new version they need to review/accept.</summary>
    Task SendDocumentUpdatedAsync(User user, string email, Document document, string documentLink);
}
