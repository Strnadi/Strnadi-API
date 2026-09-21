namespace Platform.Shared.Kernel.Configuration;

public interface IFileStorageSettings
{
    string Provider { get; } // "Local" / "S3"
    
    string? RootPath { get; } // for "Local"
    
    string? S3Bucket { get; }
    string? S3Region { get; }
    string? S3ServiceUrl { get; }
    string? S3AccessKey { get; }
    string? S3SecretKey { get; }
    bool S3ForcePathStyle { get; }
}
