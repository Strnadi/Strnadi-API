using Microsoft.AspNetCore.Identity;

namespace Administration.Domain.Entities;

public class User : IdentityUser<Guid>
{
    public override Guid Id { get; set; } = Guid.CreateVersion7();

    public string FirstName { get; set; }
    
    public string LastName { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public int? PostCode { get; set; }
    
    public string? City { get; set; } 
    
    public bool Legacy { get; set; }

    public bool Deleted { get; set; }

    public string? ProfilePhotoPath { get; set; }

    public string? ProfilePhotoFormat { get; set; }
}