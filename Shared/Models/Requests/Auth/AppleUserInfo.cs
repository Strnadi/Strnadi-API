namespace Shared.Models.Requests.Auth;

public class AppleUserInfo
{
    public NameInfo Name { get; set; }

    public string Email { get; set; }

    public class NameInfo
    {
        public string FirstName { get; set; }
        
        public string LastName { get; set; }
    }
}