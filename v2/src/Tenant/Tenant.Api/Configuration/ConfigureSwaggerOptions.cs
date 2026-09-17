using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Tenant.Api.Configuration;

// Generates one Swagger doc per registered API version instead of a single hardcoded "v1" doc -
// so adding a real v2 later is just a new [ApiVersion] somewhere, not a second migration of
// Program.cs. Today there's only one version, so this produces the same single "v1" doc the
// hardcoded SwaggerDoc("v1", ...) call used to.
public class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider) : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "Strnadi API",
                Version = description.ApiVersion.ToString()
            });
        }
    }
}
