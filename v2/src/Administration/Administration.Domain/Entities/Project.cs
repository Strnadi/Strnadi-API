using System.ComponentModel.DataAnnotations;
using Administration.Domain.Entities.Enums;

namespace Administration.Domain.Entities;

public class Project
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Name { get; set; }

    public string? Description { get; set; }

    // https://strnadi.cz - the project's frontend origin. Feeds the CORS allow-list and the
    // OpenIddict client's redirect URIs at startup (see Program.cs) for every project except
    // Draft/Rejected ones - keep that filter in sync if this field's meaning ever changes.
    public string Domain { get; set; }

    // Informational only - nothing in Program.cs's CORS/OpenIddict sync reads this.
    public string? ApiDomain { get; set; }

    public ProjectState State { get; set; }

    public string? PhotoPath { get; set; }

    public string? PhotoFormat { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? RejectionReason { get; set; }
}

