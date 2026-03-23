namespace Conspectare.Services.Interfaces;

public interface ICanonicalOutputJsonService
{
    Task<string> UploadAsync(long tenantId, long documentId, string json, CancellationToken ct = default);
    Task<string> DownloadAsync(string s3Key, CancellationToken ct = default);
}
