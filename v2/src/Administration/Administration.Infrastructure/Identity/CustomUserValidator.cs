using Administration.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Administration.Infrastructure.Identity;

public class CustomUserValidator : IUserValidator<User>
{
    public async Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user)
    {
        var errors = new List<IdentityError>();

        if (!string.IsNullOrEmpty(user.UserName))
        {
            var owner = await manager.FindByNameAsync(user.UserName);
            if (owner != null && !owner.Id.Equals(user.Id))
                errors.Add(new IdentityError() { Code = "DuplicateUserName", Description = $"Username '{user.UserName}' is already taken. "});
        }
        
        if (string.IsNullOrEmpty(user.Email))
            errors.Add(new IdentityError() { Code = "InvalidEmail", Description = "Email is required. "});
        else
        {
            var owner = await manager.FindByEmailAsync(user.Email);
            if (owner != null && !owner.Id.Equals(user.Id))
                errors.Add(new IdentityError() { Code = "DuplicateEmail", Description = $"Email '{user.Email}' is already taken. "});
        }

        return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed(errors.ToArray());
    }
}