namespace Strnadi.Domain.Services;

public interface ITokenService
{
    string GenerateToken(int userId, string email, string role);

    bool ValidateToken(string token, out int userId);
}
