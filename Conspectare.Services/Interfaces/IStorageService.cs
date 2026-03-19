namespace Conspectare.Services.Interfaces;

public interface IStorageService
{
    Task<string> UploadAsync(string key, Stream data, string contentType, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
