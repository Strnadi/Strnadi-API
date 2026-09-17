namespace Administration.Domain.Configuration;

// Our own reference Tenant.Api deployment - used to fetch the "real" capability manifest and
// OpenAPI spec that a third-party project's own API is compared against (see
// Pages/Dashboard/Projects/Details.cshtml.cs). Not to be confused with a specific project's own
// ApiDomain, which is untrusted third-party input.
public interface ITenantApiSettings
{
    string BaseUrl { get; }
}
