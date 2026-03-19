using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Conspectare.Infrastructure.Settings;
using Conspectare.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Conspectare.Services.Infrastructure;

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

        if (!string.IsNullOrEmpty(settings.ServiceUrl))
        {
            config.ServiceURL = settings.ServiceUrl;
            config.ForcePathStyle = true;
        }

        _s3 = new AmazonS3Client(credentials, config);
    }

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
