namespace Auth.Models;

public class SignUpRequest
{
    public string? Nickname { get; set; }
    
    public string Email { get; set; }
    
    public string Password { get; set; }
    
    public string FirstName { get; set; }
    
    public string LastName { get; set; }
}