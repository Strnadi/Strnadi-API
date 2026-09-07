namespace Tenant.Application.Auth;

public record SignUpRequest(
    string Email,
    string FirstName,
    string LastName,
    string? Nickname,
    string? Password,
    int? PostCode,
    string? City,
    bool Consent,
    // Identifies which privacy-policy/terms text the client actually displayed and the user
    // agreed to (e.g. a date or semantic version baked into that app build's ToS screen) - the
    // server can't know this on its own, since different client versions may show different text.
    string ConsentVersion,
    string? AppleId,
    string? GoogleId);
