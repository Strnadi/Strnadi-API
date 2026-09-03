using Administration.Api.Auth;
using Administration.Api.Components;
using Administration.Api.ExceptionHandling;
using Administration.Application.Extensions;
using Administration.Infrastructure.Extensions;
using Administration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using ServiceDefaults;

LoadEnvFile(Path.Combine(AppContext.BaseDirectory, ".env.development"));

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddControllers();

builder.Services.AddInfrastructure();
builder.Services.AddApplication();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<AdminDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AdminDbContext>())
    .AddServer(options =>
    {
        options.SetTokenEndpointUris("connect/token");

        options.AllowPasswordFlow();
        options.AllowRefreshTokenFlow();
        options.AllowCustomFlow(AuthConstants.GoogleGrantType);
        options.AllowCustomFlow(AuthConstants.AppleGrantType);

        // Dev-only ephemeral certs, regenerated on every restart — replace with persisted
        // certificates before any real deployment (see docs/rewrite-plan.md follow-up).
        options.AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();

        options.DisableAccessTokenEncryption();

        options.UseAspNetCore()
            .EnableTokenEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("ManageUsers", policy => policy.RequireClaim(AuthConstants.PermissionClaimType, "administration.users.manage"))
    .AddPolicy("ManageRoles", policy => policy.RequireClaim(AuthConstants.PermissionClaimType, "administration.roles.manage"));

builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AdminDbContext>().Database.MigrateAsync();
    await SeedOpenIddictClientAsync(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapControllers();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();

// Loads KEY=VALUE pairs from a .env file into the process environment so ASP.NET Core's
// built-in environment-variable configuration provider picks them up (Jwt__SecretKey ->
// config key "Jwt:SecretKey"). Variables already set in the environment take precedence.
static void LoadEnvFile(string path)
{
    if (!File.Exists(path))
        return;

    foreach (var line in File.ReadAllLines(path))
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            continue;

        var separatorIndex = trimmed.IndexOf('=');
        if (separatorIndex < 0)
            continue;

        var key = trimmed[..separatorIndex].Trim();
        var value = trimmed[(separatorIndex + 1)..].Trim().Trim('"');

        if (Environment.GetEnvironmentVariable(key) is null)
            Environment.SetEnvironmentVariable(key, value);
    }
}

// Every /connect/token request needs a registered OpenIddict client to belong to — without
// this, every token request fails with invalid_client. One public client for now; managing
// several (mobile/web/admin-panel with different allowed flows) is Infrastructure Manager work.
static async Task SeedOpenIddictClientAsync(IServiceProvider services)
{
    var applicationManager = services.GetRequiredService<IOpenIddictApplicationManager>();

    if (await applicationManager.FindByClientIdAsync(AuthConstants.ClientId) is not null)
        return;

    await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
    {
        ClientId = AuthConstants.ClientId,
        ClientType = OpenIddictConstants.ClientTypes.Public,
        Permissions =
        {
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.GrantTypes.Password,
            OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
            OpenIddictConstants.Permissions.Prefixes.GrantType + AuthConstants.GoogleGrantType,
            OpenIddictConstants.Permissions.Prefixes.GrantType + AuthConstants.AppleGrantType
        }
    });
}