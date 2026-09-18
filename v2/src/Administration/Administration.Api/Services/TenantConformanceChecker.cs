using System.Text.Json;
using Administration.Domain.Configuration;
using Microsoft.OpenApi;

namespace Administration.Api.Services;

public interface ITenantConformanceChecker
{
    // Raw response body from {apiDomain}/v1/.well-known/capabilities, or null if the host didn't
    // respond with a usable manifest. Not validated/parsed here - the caller just stores/displays it.
    Task<string?> FetchCapabilitiesAsync(string apiDomain, CancellationToken cancellationToken = default);

    // "METHOD /path" entries present in our reference Tenant.Api's spec but missing from
    // apiDomain's own spec. Empty list = fully compatible; a single explanatory entry if either
    // spec couldn't be fetched/parsed at all.
    Task<IReadOnlyList<string>> CheckApiConformanceAsync(string apiDomain, CancellationToken cancellationToken = default);

    // Our own reference deployment's declared features, for comparing against a project's stored
    // manifest. Unlike FetchCapabilitiesAsync(apiDomain), this hits our own reliable deployment,
    // so it's cheap enough to call on every Details page view.
    Task<IReadOnlyList<string>?> FetchOwnFeaturesAsync(CancellationToken cancellationToken = default);

    // Extracts the "features" array out of a stored/fetched capabilities manifest. Returns an
    // empty list for null/malformed input rather than throwing - a bad manifest is a display
    // concern (0 declared features), not a fatal error.
    IReadOnlyList<string> ParseFeatures(string? capabilitiesJson);
}

// Both operations only ever compare declared surface (a manifest, an OpenAPI path/method list) -
// never calls a project's actual business endpoints or validates request/response bodies. See
// the plan this was built from: no runtime handshake, no CLI, just a spec diff.
public class TenantConformanceChecker(IHttpClientFactory httpClientFactory, ITenantApiSettings tenantApiSettings)
    : ITenantConformanceChecker
{
    private const string HttpClientName = "TenantApiClient";

    public async Task<string?> FetchCapabilitiesAsync(string apiDomain, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        try
        {
            var response = await client.GetAsync($"{apiDomain.TrimEnd('/')}/v1/.well-known/capabilities", cancellationToken);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(cancellationToken) : null;
        }
        catch (Exception)
        {
            // Untrusted third-party host - network errors, timeouts, TLS failures are all
            // expected failure modes here, not exceptional ones.
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> CheckApiConformanceAsync(string apiDomain, CancellationToken cancellationToken = default)
    {
        if (tenantApiSettings.BaseUrl is not { } baseUrl)
            return ["The TenantApi:BaseUrl setting is not configured - cannot fetch our own reference specification to compare against."];

        var client = httpClientFactory.CreateClient(HttpClientName);

        var reference = await FetchSpecAsync(client, $"{baseUrl}/swagger/v1/swagger.json", cancellationToken);
        if (reference is null)
            return ["Could not fetch our own reference specification - check the TenantApi:BaseUrl setting."];

        var project = await FetchSpecAsync(client, $"{apiDomain.TrimEnd('/')}/swagger/v1/swagger.json", cancellationToken);
        if (project is null)
            return ["Could not fetch or parse an OpenAPI specification from this project's API domain."];

        var missing = new List<string>();
        foreach (var (path, referenceItem) in reference.Paths)
        {
            var hasPath = project.Paths.TryGetValue(path, out var projectItem);
            foreach (var method in referenceItem.Operations.Keys)
            {
                if (!hasPath || !projectItem!.Operations.ContainsKey(method))
                    missing.Add($"{method.ToString().ToUpperInvariant()} {path}");
            }
        }

        return missing;
    }

    public async Task<IReadOnlyList<string>?> FetchOwnFeaturesAsync(CancellationToken cancellationToken = default)
    {
        if (tenantApiSettings.BaseUrl is not { } baseUrl)
            return null;

        var json = await FetchCapabilitiesAsync(baseUrl, cancellationToken);
        return json is null ? null : ParseFeatures(json);
    }

    public IReadOnlyList<string> ParseFeatures(string? capabilitiesJson)
    {
        if (string.IsNullOrWhiteSpace(capabilitiesJson))
            return [];

        try
        {
            using var document = JsonDocument.Parse(capabilitiesJson);
            if (!document.RootElement.TryGetProperty("features", out var featuresElement)
                || featuresElement.ValueKind != JsonValueKind.Array)
                return [];

            return featuresElement.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(s => s!)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static async Task<OpenApiDocument?> FetchSpecAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await OpenApiDocument.LoadAsync(content, cancellationToken: cancellationToken);
            return result.Document;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
