/*
 * Copyright (C) 2024 Stanislav Motsnyi
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using System.Reflection;
using Achievements;
using Articles;
using Auth;
using Devices;
using Dictionary;
using Email;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using Photos;
using Recordings;
using Repository;
using Shared.Extensions;
using Swashbuckle.AspNetCore.Swagger;
using Users;
using Utils;

namespace Host;

class Program
{
    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var configuration = builder.Configuration;

        ConfigureServices(builder, configuration);
        
        var app = builder.Build();

        ConfigureApp(app, configuration);
        
        app.Run();
    }

    static void ConfigureServices(WebApplicationBuilder builder, ConfigurationManager configuration)
    {
        var defaultCorsPolicy = configuration["CORS:Default"]
                                ?? throw new InvalidOperationException("CORS:Default configuration is required.");

        builder.Services.AddMemoryCache();
        builder.Services.AddLogging();
        builder.Services.AddHttpClient();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Strnadi API",
                Version = "1.0.0",
                Description =
                    "HTTP API used by the Strnadi applications to manage user accounts, bird recordings, articles and other shared resources."
            });
            options.AddSecurityDefinition("bearerAuth", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme."
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "bearerAuth"
                        }
                    },
                    []
                }
            });

            foreach (string xmlDocumentationFilePath in GetXmlDocumentationFilePaths())
            {
                options.IncludeXmlComments(xmlDocumentationFilePath, includeControllerXmlComments: true);
            }
        });
        builder.Services.AddTools(configuration);
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(UsersController).Assembly)
            .AddApplicationPart(typeof(AuthController).Assembly)
            .AddApplicationPart(typeof(RecordingsController).Assembly)
            .AddApplicationPart(typeof(DevicesController).Assembly)
            .AddApplicationPart(typeof(UtilsController).Assembly)
            .AddApplicationPart(typeof(PhotosController).Assembly)
            .AddApplicationPart(typeof(ArticlesController).Assembly)
            .AddApplicationPart(typeof(DictionaryController).Assembly)
            .AddApplicationPart(typeof(AchievementsController).Assembly);
        builder.Services.AddRepositories();
        builder.Services.AddEmailServices();
        builder.Services.AddAuthServices();
        builder.Services.AddCors(corsOptions =>
        {
            corsOptions.AddPolicy(defaultCorsPolicy, policyBuilder =>
            {
                policyBuilder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });
    }

    static void ConfigureApp(WebApplication app, IConfiguration configuration)
    {
        var defaultCorsPolicy = configuration["CORS:Default"]
                                ?? throw new InvalidOperationException("CORS:Default configuration is required.");

        app.UseCors(defaultCorsPolicy);
        app.UseHttpsRedirection();
        app.UseRouting();
        app.MapControllers();
        app.UseSwagger();
        app.MapGet("/swagger/StrnadiAPI-openapi.yaml", (ISwaggerProvider swaggerProvider) =>
            Results.Text(SerializeOpenApiAsYaml(swaggerProvider.GetSwagger("v1")), "application/yaml"));
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Strnadi API");
            options.RoutePrefix = string.Empty;
            options.DocumentTitle = "Strnadi API - Swagger";
        });
    }

    static IEnumerable<string> GetXmlDocumentationFilePaths()
    {
        var basePath = AppContext.BaseDirectory;
        string[] assemblyNames =
        [
            Assembly.GetExecutingAssembly().GetName().Name!,
            "Achievements",
            "Articles",
            "Auth",
            "Devices",
            "Photos",
            "Recordings",
            "Repository",
            "Shared",
            "Users",
            "Utils"
        ];

        return assemblyNames
            .Select(assemblyName => Path.Combine(basePath, $"{assemblyName}.xml"))
            .Where(File.Exists);
    }

    static string SerializeOpenApiAsYaml(Microsoft.OpenApi.Models.OpenApiDocument openApiDocument)
    {
        using var writer = new StringWriter();
        var yamlWriter = new OpenApiYamlWriter(writer);
        openApiDocument.SerializeAsV3(yamlWriter);
        return writer.ToString();
    }
}
