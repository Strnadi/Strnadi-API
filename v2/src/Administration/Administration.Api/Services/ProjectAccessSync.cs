using Administration.Domain.Entities.Enums;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace Administration.Api.Services;

public interface IProjectAccessSync
{
    Task SyncAsync(CancellationToken cancellationToken = default);
}

// Keeps the shared "strnadi-app" OpenIddict client's redirect URIs and the ProjectOrigins CORS
// policy in sync with which projects are currently approved (anything except Draft/Rejected -
// unreviewed, publicly-submitted requests, see Pages/Dashboard/Projects/Create.cshtml.cs). Called
// once at startup (Program.cs) and again from Projects/Edit.cshtml.cs whenever an admin changes a
// project's State or Domain, so approving a request takes effect immediately instead of only
// after the app happens to restart.
public class ProjectAccessSync(
    AdminDbContext db,
    IOpenIddictApplicationManager applicationManager,
    IOptions<CorsOptions> corsOptions) : IProjectAccessSync
{
    public const string ClientId = "strnadi-app";
    public const string CorsPolicyName = "ProjectOrigins";

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        var domains = await db.Projects
            .Where(p => p.Domain != null && p.Domain != ""
                && p.State != ProjectState.Draft && p.State != ProjectState.Rejected)
            .Select(p => p.Domain)
            .ToListAsync(cancellationToken);

        await SyncOpenIddictClientAsync(domains, cancellationToken);

        // CorsOptions.Value is the same singleton instance the CORS middleware reads the named
        // policy from on every request - overwriting the entry here takes effect immediately,
        // no restart needed.
        corsOptions.Value.AddPolicy(CorsPolicyName, policy =>
            policy.WithOrigins(domains.Select(d => d.TrimEnd('/')).ToArray())
                .AllowAnyMethod()
                .AllowAnyHeader());
    }

    private async Task SyncOpenIddictClientAsync(IReadOnlyList<string> domains, CancellationToken cancellationToken)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = ClientId,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            RedirectUris = { new Uri("com.delta.strnadi://auth/callback") },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.Prefixes.GrantType + OpenIddictConstants.GrantTypes.TokenExchange,
                OpenIddictConstants.Permissions.ResponseTypes.Code
            },
        };

        foreach (var domain in domains)
        {
            descriptor.RedirectUris.Add(new Uri($"{domain.TrimEnd('/')}/ucet/prihlaseni"));
            descriptor.RedirectUris.Add(new Uri($"{domain.TrimEnd('/')}/ucet/registrace"));
        }

        var existing = await applicationManager.FindByClientIdAsync(ClientId, cancellationToken);
        if (existing is null)
            await applicationManager.CreateAsync(descriptor, cancellationToken);
        else
            await applicationManager.UpdateAsync(existing, descriptor, cancellationToken);
    }
}
