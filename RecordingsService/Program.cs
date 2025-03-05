using Shared.Communication;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddScoped<RecordingsControllerClient>();

builder.Services.AddControllers();
builder.Services.AddCors(corsOptions =>
{
    corsOptions.AddPolicy(configuration["CORS:Default"], policyBuilder =>
    {
        policyBuilder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();
app.MapControllers();
app.Run();