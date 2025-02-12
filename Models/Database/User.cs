using Models.Database.Enums;

namespace Models.Database;

public class User
{
    public int Id { get; set; }
    
    public UserRole Role { get; set; }

    public string? Nickname { get; set; }

    public string Email { get; set; }
    
    public string Password { get; set; }
    
    public string FirstName { get; set; } 

    public string LastName { get; set; }

    public DateTime CreationDate { get; set; } 

    public bool? IsEmailVerified { get; set; }

    public bool? Consent { get; set; }
}