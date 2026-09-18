namespace Administration.Domain.Configuration;

// Our own reference Tenant.Api deployment - used to fetch the "real" capability manifest and
// OpenAPI spec that a third-party project's own API is compared against (see
// Pages/Dashboard/Projects/Details.cshtml.cs). Not to be confused with a specific project's own
// ApiDomain, which is untrusted third-party input.
//
// Nullable, unlike most other settings interfaces here (e.g. IOpenIddictSettings) - those back a
// hard startup dependency where failing fast is correct, but this only backs an optional,
// per-request conformance-check feature. Missing config here must be a handled "not configured
// yet" state for the caller, not an exception that 500s an unrelated page render.
public interface ITenantApiSettings
{
    string? BaseUrl { get; }
}
