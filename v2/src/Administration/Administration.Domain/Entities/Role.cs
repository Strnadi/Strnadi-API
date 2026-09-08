using Microsoft.AspNetCore.Identity;

namespace Administration.Domain.Entities;

public class Role : IdentityRole<Guid>
{
    public override Guid Id { get; set; } = Guid.CreateVersion7();
    
    public Guid ProjectId { get; set; }
    
    public string? Description { get; set; }
}