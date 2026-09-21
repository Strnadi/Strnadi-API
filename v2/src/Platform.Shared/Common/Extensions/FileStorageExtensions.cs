using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Platform.Shared.Infrastructure.Configuration;
using Platform.Shared.Infrastructure.Storage;
using Platform.Shared.Kernel.Configuration;
using Platform.Shared.Kernel.Services;

namespace Platform.Shared.Common.Extensions;

public static class FileStorageExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddFileStorage()
        {
            services.AddSingleton<IFileStorageSettings, FileStorageSettings>();
            services.AddSingleton<IAmazonS3>(sp =>
            {
                var settings = sp.GetRequiredService<IFileStorageSettings>();

                var config = new AmazonS3Config
                {
                    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                };

                if (!string.IsNullOrEmpty(settings.S3ServiceUrl))
                {
                    config.ServiceURL = settings.S3ServiceUrl;
                    config.AuthenticationRegion = settings.S3Region;
                    config.ForcePathStyle = settings.S3ForcePathStyle;
                }
                else
                {
                    config.RegionEndpoint = RegionEndpoint.GetBySystemName(settings.S3Region);
                }

                return string.IsNullOrEmpty(settings.S3AccessKey)
                    ? new AmazonS3Client(config)
                    : new AmazonS3Client(settings.S3AccessKey, settings.S3SecretKey, config);
            });

            services.AddSingleton<IFileStorage>(sp =>
            {
                var settings = sp.GetRequiredService<IFileStorageSettings>();

                return settings.Provider.ToLowerInvariant() switch
                {
                    "local" => ActivatorUtilities.CreateInstance<LocalStorage>(sp),
                    "s3" => ActivatorUtilities.CreateInstance<S3Storage>(sp),
                    _ => throw new InvalidOperationException(
                        $"Unknown Storage:Provider '{settings.Provider}'. Expected 'Local' or 'S3'.")
                };
            });

            return services;
        }
    }
}