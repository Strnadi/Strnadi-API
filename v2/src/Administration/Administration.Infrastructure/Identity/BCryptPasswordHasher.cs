using Administration.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Administration.Infrastructure.Identity;

public class BCryptPasswordHasher : IPasswordHasher<User>
{
    public string HashPassword(User user, string password) => BCrypt.Net.BCrypt.HashPassword(password);
    
    public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword) => 
        BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword)
            ? PasswordVerificationResult.Success 
            : PasswordVerificationResult.Failed;
}