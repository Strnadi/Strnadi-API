using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Platform.Shared.Kernel.Configuration;
using Platform.Shared.Kernel.Services;

namespace Platform.Shared.Infrastructure.Storage;

public class S3Storage(IAmazonS3 s3, IFileStorageSettings settings) : IFileStorage
{
    public async Task<string> SaveAsync(string relativePath, byte[] content, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(content);
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = settings.S3Bucket!,
            Key = relativePath,
            InputStream = stream,
        }, cancellationToken);

        return relativePath;
    }

    public async Task<byte[]?> ReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await s3.GetObjectAsync(settings.S3Bucket, relativePath, cancellationToken);
            using var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms, cancellationToken);
            return ms.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        await s3.DeleteObjectAsync(settings.S3Bucket, relativePath, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string relativePath)
    {
        try
        {
            await s3.GetObjectMetadataAsync(settings.S3Bucket, relativePath);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}