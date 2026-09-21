using Microsoft.Extensions.Configuration;
using Platform.Shared.Kernel.Configuration;

namespace Platform.Shared.Infrastructure.Configuration;

public class FileStorageSettings(IConfiguration configuration) : IFileStorageSettings
{
    public string Provider => configuration["Storage:Provider"] ?? "Local";

    public string RootPath => configuration["Storage:RootPath"]
                              ?? AppContext.BaseDirectory;

    public string? S3Bucket => configuration["Storage:S3:Bucket"];
    public string? S3Region => configuration["Storage:S3:Region"];
    public string? S3ServiceUrl => configuration["Storage:S3:ServiceUrl"];
    public string? S3AccessKey => configuration["Storage:S3:AccessKey"];
    public string? S3SecretKey => configuration["Storage:S3:SecretKey"];
    public bool S3ForcePathStyle => bool.Parse(configuration["Storage:S3:ForcePathStyle"] ?? "false");
}
