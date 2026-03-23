using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Conspectare.Client.Models;
using Microsoft.Extensions.Logging;

namespace Conspectare.Client;

public class ConspectareClient : IConspectareClient
{
    private const string BasePath = "/api/v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;
    private readonly ILogger<ConspectareClient> _logger;

    public ConspectareClient(HttpClient http, ILogger<ConspectareClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<UploadAcceptedResponse> SubmitDocumentAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string? clientReference = null,
        string? fiscalCode = null,
        string? metadata = null,
        CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);

        if (clientReference is not null)
            content.Add(new StringContent(clientReference), "clientReference");
        if (fiscalCode is not null)
            content.Add(new StringContent(fiscalCode), "fiscalCode");
        if (metadata is not null)
            content.Add(new StringContent(metadata), "metadata");

        using var response = await _http.PostAsync($"{BasePath}/documents", content, ct);
        return await ReadResponseAsync<UploadAcceptedResponse>(response, ct);
    }

    public async Task<BatchUploadResponse> SubmitBatchAsync(
        IReadOnlyList<BatchFileInput> files,
        string? clientReference = null,
        string? fiscalCode = null,
        string? metadata = null,
        CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();

        foreach (var file in files)
        {
            var streamContent = new StreamContent(file.Stream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            content.Add(streamContent, "files", file.FileName);
        }

        if (clientReference is not null)
            content.Add(new StringContent(clientReference), "clientReference");
        if (fiscalCode is not null)
            content.Add(new StringContent(fiscalCode), "fiscalCode");
        if (metadata is not null)
            content.Add(new StringContent(metadata), "metadata");

        using var response = await _http.PostAsync($"{BasePath}/documents/batch", content, ct);
        return await ReadResponseAsync<BatchUploadResponse>(response, ct);
    }

    public async Task<DocumentResponse> GetDocumentAsync(long documentId, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"{BasePath}/documents/{documentId}", ct);
        return await ReadResponseAsync<DocumentResponse>(response, ct);
    }

    public async Task<DocumentListResponse> ListDocumentsAsync(
        string? status = null,
        string? search = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var queryParams = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

        if (status is not null)
            queryParams.Add($"status={Uri.EscapeDataString(status)}");
        if (search is not null)
            queryParams.Add($"search={Uri.EscapeDataString(search)}");
        if (dateFrom.HasValue)
            queryParams.Add($"dateFrom={dateFrom.Value.ToString("O", CultureInfo.InvariantCulture)}");
        if (dateTo.HasValue)
            queryParams.Add($"dateTo={dateTo.Value.ToString("O", CultureInfo.InvariantCulture)}");

        var query = string.Join("&", queryParams);
        using var response = await _http.GetAsync($"{BasePath}/documents?{query}", ct);
        return await ReadResponseAsync<DocumentListResponse>(response, ct);
    }

    public async Task<Stream> DownloadRawAsync(long documentId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"{BasePath}/documents/{documentId}/raw", ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            response.Dispose();
            throw new ConspectareApiException(
                response.StatusCode,
                body,
                $"GET {BasePath}/documents/{documentId}/raw failed with {(int)response.StatusCode}");
        }
        return await response.Content.ReadAsStreamAsync(ct);
    }

    public async Task<DocumentResponse> RetryDocumentAsync(long documentId, CancellationToken ct = default)
    {
        using var response = await _http.PostAsync($"{BasePath}/documents/{documentId}/retry", null, ct);
        return await ReadResponseAsync<DocumentResponse>(response, ct);
    }

    public async Task<DocumentResponse> ResolveDocumentAsync(long documentId, ResolveDocumentRequest request, CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync($"{BasePath}/documents/{documentId}/resolve", request, JsonOptions, ct);
        return await ReadResponseAsync<DocumentResponse>(response, ct);
    }

    public async Task<DocumentResponse> UpdateCanonicalOutputAsync(long documentId, UpdateCanonicalOutputRequest request, CancellationToken ct = default)
    {
        using var response = await _http.PatchAsJsonAsync($"{BasePath}/documents/{documentId}/canonical-output", request, JsonOptions, ct);
        return await ReadResponseAsync<DocumentResponse>(response, ct);
    }

    private async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Conspectare API returned {StatusCode} for {Method} {Uri}: {Body}",
                (int)response.StatusCode,
                response.RequestMessage?.Method,
                response.RequestMessage?.RequestUri,
                body);

            throw new ConspectareApiException(
                response.StatusCode,
                body,
                $"Conspectare API returned {(int)response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<T>(body, JsonOptions);
        if (result is null)
            throw new ConspectareApiException(
                response.StatusCode,
                body,
                $"Failed to deserialize response as {typeof(T).Name}");

        return result;
    }
}
