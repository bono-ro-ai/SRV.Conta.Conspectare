using System.Text;
using Conspectare.Services.Infrastructure;
using Conspectare.Services.Interfaces;

namespace Conspectare.Services;

public class CanonicalOutputJsonService : ICanonicalOutputJsonService
{
    private readonly IStorageService _storageService;

    public CanonicalOutputJsonService(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public async Task<string> UploadAsync(long tenantId, long documentId, string json, CancellationToken ct = default)
    {
        var s3Key = S3KeyBuilder.Output(tenantId, documentId, "canonical_output.json");
        var bytes = Encoding.UTF8.GetBytes(json);
        using var stream = new MemoryStream(bytes);
        await _storageService.UploadAsync(s3Key, stream, "application/json", ct);
        return s3Key;
    }

    public async Task<string> DownloadAsync(string s3Key, CancellationToken ct = default)
    {
        await using var stream = await _storageService.DownloadAsync(s3Key, ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(ct);
    }
}
