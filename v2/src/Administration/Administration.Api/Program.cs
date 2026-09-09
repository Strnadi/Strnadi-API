using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography.X509Certificates;
using Administration.Domain.Configuration;
using Administration.Domain.Entities;
using Administration.Domain.Services;
using Administration.Infrastructure.Configuration;
using Administration.Infrastructure.Email;
using Administration.Infrastructure.Identity;
using Administration.Infrastructure.Persistence;
using Administration.Infrastructure.Security;
using Administration.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Abstractions;
using ServiceDefaults;

const string clientId = "strnadi-app";

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddTrustedReverseProxy();
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath));
builder.Services.AddHealthChecks().AddDbContextCheck<AdminDbContext>();

builder.Services.AddDbContext<AdminDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddSingleton<IEncryptionSettings, EncryptionSettings>();
builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();

builder.Services.AddSingleton<IGoogleAuthSettings, GoogleAuthSettings>();
builder.Services.AddSingleton<IAppleAuthSettings, AppleAuthSettings>();
builder.Services.AddSingleton<IAuthSettings, AuthSettings>();

builder.Services.AddSingleton<ISmtpSettings, SmtpSettings>();
builder.Services.AddScoped<IEmailSender<User>, SmtpEmailSender>();

builder.Services.AddSingleton<IFileStorageSettings, LocalStorageSettings>();
builder.Services.AddSingleton<IFileStorage, LocalStorage>();

builder.Services.AddIdentityCore<User>(o => o.User.RequireUniqueEmail = true)
    .AddRoles<Role>()
    .AddSignInManager()
    .AddDefaultTokenProviders()
    .AddEntityFrameworkStores<AdminDbContext>();

builder.Services.RemoveAll<IPasswordHasher<User>>();
builder.Services.AddScoped<IPasswordHasher<User>, BCryptPasswordHasher>();

builder.Services.RemoveAll<IUserValidator<User>>();
builder.Services.AddScoped<IUserValidator<User>, CustomUserValidator>();

var googleAuthSettings = new GoogleAuthSettings(builder.Configuration);
var appleAuthSettings = new AppleAuthSettings(builder.Configuration);

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.LoginPath = "/account/login";
    })
    .AddGoogle(options =>
    {
        options.ClientId = googleAuthSettings.ClientId;
        options.ClientSecret = googleAuthSettings.ClientSecret;
        options.SignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddApple(options =>
    {
        options.ClientId = appleAuthSettings.ClientId;
        options.TeamId = appleAuthSettings.TeamId;
        options.KeyId = appleAuthSettings.KeyId;
        options.PrivateKey = (keyId, ct) => Task.FromResult(appleAuthSettings.P8PrivateKey.AsMemory());
        options.SignInScheme = IdentityConstants.ExternalScheme;
    });

builder.Services.AddAuthorization();

builder.Services.AddOpenIddict()
    .AddCore(o => o.UseEntityFrameworkCore().UseDbContext<AdminDbContext>())
    .AddServer(o =>
    {
        o.SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .SetUserInfoEndpointUris("/connect/user-info")
            .SetEndSessionEndpointUris("/connect/logout")
            .AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange()
            .AllowRefreshTokenFlow()
            .AllowCustomFlow(OpenIddictConstants.GrantTypes.TokenExchange)
            .UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableUserInfoEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough();

        if (builder.Environment.IsDevelopment())
        {
            o.AddDevelopmentEncryptionCertificate()
                .AddDevelopmentSigningCertificate();
        }
        else
        {
            o.AddSigningCertificate(LoadCertificate(builder.Configuration, "OAuth:Signing"))
                .AddEncryptionCertificate(LoadCertificate(builder.Configuration, "OAuth:Encryption"));
        }
    });

builder.Services.AddControllers();
builder.Services.AddRazorPages();

var app = builder.Build();
app.UseForwardedHeaders();

using (var scope = app.Services.CreateScope())
    await SyncOpenIddictClientAsync(scope.ServiceProvider);

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();
app.MapDefaultEndpoints();

app.Run();

static X509Certificate2 LoadCertificate(IConfiguration configuration, string configSection) =>
    X509CertificateLoader.LoadPkcs12(
        Convert.FromBase64String(configuration[$"{configSection}:Pfx"]!),
        configuration[$"{configSection}:Password"]);

// Redirect URIs for web are derived from Project.Domain, not hardcoded - each Project owns a
// domain (strnadi.cz, sarancata.cz, ...), and its OAuth callback lives at {Domain}/auth/callback.
// Call this again (not just at startup) whenever a Project's Domain is created/changed, so a new
// project's domain becomes valid without waiting for a restart.
static async Task SyncOpenIddictClientAsync(IServiceProvider services)
{
    var applicationManager = services.GetRequiredService<IOpenIddictApplicationManager>();
    var db = services.GetRequiredService<AdminDbContext>();

    var domains = await db.Projects
        .Where(p => p.Domain != null && p.Domain != "")
        .Select(p => p.Domain)
        .ToListAsync();

    var descriptor = new OpenIddictApplicationDescriptor
    {
        ClientId = clientId,
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
        }
    };

    foreach (var domain in domains)
    {
        descriptor.RedirectUris.Add(new Uri($"{domain.TrimEnd('/')}/ucet/prihlaseni"));
        descriptor.RedirectUris.Add(new Uri($"{domain.TrimEnd('/')}/ucet/registrace"));
    }

    var existing = await applicationManager.FindByClientIdAsync(clientId);
    if (existing is null)
        await applicationManager.CreateAsync(descriptor);
    else
        await applicationManager.UpdateAsync(existing, descriptor);
}
