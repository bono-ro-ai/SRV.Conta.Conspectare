using System.Text;
using Conspectare.Services.Infrastructure;
using Conspectare.Services.Interfaces;

namespace Conspectare.Services;

/// <summary>
/// Handles persistence of canonical output JSON to and from S3 storage.
/// Keys are constructed via <see cref="S3KeyBuilder"/> to ensure a consistent object layout.
/// </summary>
public class CanonicalOutputJsonService : ICanonicalOutputJsonService
{
    private readonly IStorageService _storageService;

    public CanonicalOutputJsonService(IStorageService storageService)
    {
        _storageService = storageService;
    }

    /// <summary>
    /// Encodes <paramref name="json"/> as UTF-8 and uploads it to S3 under the standard
    /// canonical-output key for the given tenant and document. Returns the S3 key on success.
    /// </summary>
    public async Task<string> UploadAsync(long tenantId, long documentId, string json, CancellationToken ct = default)
    {
        var s3Key = S3KeyBuilder.Output(tenantId, documentId, "canonical_output.json");
        var bytes = Encoding.UTF8.GetBytes(json);

        using var stream = new MemoryStream(bytes);
        await _storageService.UploadAsync(s3Key, stream, "application/json", ct);

        return s3Key;
    }

    /// <summary>
    /// Downloads the canonical output JSON from S3 at <paramref name="s3Key"/>
    /// and returns it as a UTF-8 string.
    /// </summary>
    public async Task<string> DownloadAsync(string s3Key, CancellationToken ct = default)
    {
        await using var stream = await _storageService.DownloadAsync(s3Key, ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(ct);
    }
}
