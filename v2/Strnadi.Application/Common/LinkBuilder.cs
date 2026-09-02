using Strnadi.Domain.Configuration;

namespace Strnadi.Application.Common;

public class LinkBuilder(IHostSettings hostSettings)
{
    public string VerificationLink(int userId, string jwt) =>
        $"{hostSettings.ApiHost}/users/{userId}/verify-email?jwt={jwt}";

    public string EmailVerificationRedirectLink(bool success) =>
        $"{hostSettings.WebHost}/ucet/email-{(success ? "" : "ne")}overen";
    
    public string PasswordResetLink(int userId, string jwt) => 
        $"{hostSettings.WebHost}/ucet/obnova-hesla?token={jwt}&userId={userId}";
}