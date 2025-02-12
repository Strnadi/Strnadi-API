namespace Models.Requests;

public class SignUpRequest
{
    public string Email { get; set; }
    
    public string? Nickname { get; set; }
    
    public string Password { get; set; }
    
    public string FirstName { get; set; }
    
    public string LastName { get; set; }
}