using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Conspectare.Infrastructure.Settings;
using Conspectare.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Conspectare.Services.Infrastructure;

/// <summary>
/// AWS S3-backed implementation of <see cref="IStorageService"/>.
/// Supports an optional <c>ServiceUrl</c> override to target S3-compatible stores (e.g. MinIO in dev/test).
/// </summary>
public class S3StorageService : IStorageService
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;
    private readonly ILogger<S3StorageService> _logger;

    public S3StorageService(IOptions<AwsSettings> options, ILogger<S3StorageService> logger)
    {
        _logger = logger;
        var settings = options.Value;
        _bucketName = settings.BucketName;

        var credentials = new BasicAWSCredentials(settings.AccessKeyId, settings.SecretAccessKey);
        var config = new AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(settings.Region)
        };

        // When a ServiceUrl is configured, switch to path-style addressing for S3-compatible endpoints.
        if (!string.IsNullOrEmpty(settings.ServiceUrl))
        {
            config.ServiceURL = settings.ServiceUrl;
            config.ForcePathStyle = true;
        }

        _s3 = new AmazonS3Client(credentials, config);
    }

    /// <summary>
    /// Uploads a stream to S3 under the given <paramref name="key"/> and returns the key on success.
    /// </summary>
    public async Task<string> UploadAsync(string key, Stream data, string contentType, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = data,
            ContentType = contentType ?? "application/octet-stream"
        };

        await _s3.PutObjectAsync(request, ct);
        _logger.LogInformation("Uploaded {Key} to bucket {Bucket}", key, _bucketName);
        return key;
    }

    /// <summary>
    /// Downloads the object at <paramref name="key"/> and returns the raw response stream.
    /// The caller is responsible for disposing the returned stream.
    /// </summary>
    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        var request = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        };

        var response = await _s3.GetObjectAsync(request, ct);
        _logger.LogInformation("Downloaded {Key} from bucket {Bucket}", key, _bucketName);
        return response.ResponseStream;
    }

    /// <summary>
    /// Returns <c>true</c> if an object with <paramref name="key"/> exists in the bucket;
    /// returns <c>false</c> for 404 responses without throwing.
    /// </summary>
    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            await _s3.GetObjectMetadataAsync(request, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <summary>
    /// Permanently deletes the object at <paramref name="key"/> from the bucket.
    /// </summary>
    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        };

        await _s3.DeleteObjectAsync(request, ct);
        _logger.LogInformation("Deleted {Key} from bucket {Bucket}", key, _bucketName);
    }

    /// <summary>
    /// Generates a pre-signed GET URL for the object at <paramref name="key"/> that expires
    /// after the specified <paramref name="expiry"/> duration.
    /// </summary>
    public Task<string> GeneratePresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow + expiry
        };

        var url = _s3.GetPreSignedURL(request);
        _logger.LogInformation("Generated presigned URL for {Key} in bucket {Bucket}", key, _bucketName);
        return Task.FromResult(url);
    }
}
