using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Conspectare.Services.ExternalIntegrations.Anaf;

/// <summary>
/// HTTP client for the Romanian ANAF VAT registry API (anaf.ro/api/v9/ws/tva).
/// Validates that a given fiscal code (CUI) is registered and active in the ANAF database.
/// </summary>
public class AnafVatValidationClient : IAnafVatValidationClient
{
    private readonly HttpClient _httpClient;
    private readonly AnafVatValidationSettings _settings;
    private readonly ILogger<AnafVatValidationClient> _logger;

    public AnafVatValidationClient(
        HttpClient httpClient,
        IOptions<AnafVatValidationSettings> options,
        ILogger<AnafVatValidationClient> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
    }

    /// <summary>
    /// Validates a Romanian fiscal code (CUI) against the ANAF VAT registry for today's date.
    /// Automatically strips the "RO" prefix if present.
    /// Retries on timeout or transient HTTP errors up to <see cref="AnafVatValidationSettings.MaxRetries"/> times
    /// with exponential-ish back-off (2 s → 5 s → 10 s).
    /// Returns a failed <see cref="AnafValidationResult"/> (never throws) on all error paths.
    /// </summary>
    public async Task<AnafValidationResult> ValidateCuiAsync(string cui, CancellationToken ct)
    {
        var numericCui = StripCuiPrefix(cui);

        if (!long.TryParse(numericCui, out var cuiNumber))
        {
            return new AnafValidationResult(
                IsValid: false,
                Cui: cui,
                CompanyName: null,
                IsInactive: false,
                ValidationError: $"CUI '{cui}' is not a valid numeric identifier");
        }

        // Romanian CUIs are 2–10 digits; anything outside that range is immediately invalid.
        if (numericCui.Length < 2 || numericCui.Length > 10)
        {
            return new AnafValidationResult(
                IsValid: false,
                Cui: cui,
                CompanyName: null,
                IsInactive: false,
                ValidationError: $"CUI '{cui}' has invalid length ({numericCui.Length} digits) — Romanian CUIs must be 2-10 digits");
        }

        // ANAF requires the lookup date in the request body; use today's UTC date.
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var requestBody = new JsonArray
        {
            new JsonObject
            {
                ["cui"] = cuiNumber,
                ["data"] = today
            }
        };

        for (var attempt = 0; attempt <= _settings.MaxRetries; attempt++)
        {
            try
            {
                return await SendRequestAsync(cui, requestBody, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // The HttpClient's own timeout fired, not the caller's cancellation.
                if (attempt >= _settings.MaxRetries)
                {
                    return new AnafValidationResult(
                        IsValid: false,
                        Cui: cui,
                        CompanyName: null,
                        IsInactive: false,
                        ValidationError: $"ANAF API timed out after {_settings.TimeoutSeconds} seconds");
                }

                _logger.LogWarning(
                    "ANAF API timed out for CUI {Cui}, retrying (attempt {Attempt}/{MaxRetries})",
                    cui, attempt + 1, _settings.MaxRetries);
            }
            catch (HttpRequestException ex) when (attempt < _settings.MaxRetries)
            {
                _logger.LogWarning(ex,
                    "ANAF API request failed for CUI {Cui}, retrying (attempt {Attempt}/{MaxRetries})",
                    cui, attempt + 1, _settings.MaxRetries);
            }
            catch (HttpRequestException)
            {
                return new AnafValidationResult(
                    IsValid: false,
                    Cui: cui,
                    CompanyName: null,
                    IsInactive: false,
                    ValidationError: "ANAF API request failed");
            }

            // Back-off: 2 s after 1st failure, 5 s after 2nd, 10 s thereafter.
            var delaySeconds = attempt switch { 0 => 2, 1 => 5, _ => 10 };
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
        }

        return new AnafValidationResult(
            IsValid: false,
            Cui: cui,
            CompanyName: null,
            IsInactive: false,
            ValidationError: "Exhausted all retry attempts");
    }

    /// <summary>
    /// Sends the prepared JSON request body to the ANAF endpoint and parses the response.
    /// Separated from the retry loop to keep each concern isolated and to allow unit-testing the HTTP call.
    /// </summary>
    internal async Task<AnafValidationResult> SendRequestAsync(
        string originalCui, JsonArray requestBody, CancellationToken ct)
    {
        using var content = new StringContent(
            requestBody.ToJsonString(),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("/api/v9/ws/tva", content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        return ParseResponse(originalCui, responseJson);
    }

    /// <summary>
    /// Parses the ANAF JSON response body and maps it to an <see cref="AnafValidationResult"/>.
    /// Returns a failed result rather than throwing on malformed JSON or missing registry entries.
    /// </summary>
    internal static AnafValidationResult ParseResponse(string originalCui, string responseJson)
    {
        JsonNode root;
        try
        {
            root = JsonNode.Parse(responseJson);
        }
        catch (JsonException)
        {
            return new AnafValidationResult(
                IsValid: false,
                Cui: originalCui,
                CompanyName: null,
                IsInactive: false,
                ValidationError: "ANAF API returned malformed JSON response");
        }

        // The ANAF API returns a "found" array; an empty array means the CUI is not registered.
        var found = root?["found"];
        if (found is not JsonArray foundArray || foundArray.Count == 0)
        {
            return new AnafValidationResult(
                IsValid: false,
                Cui: originalCui,
                CompanyName: null,
                IsInactive: false,
                ValidationError: $"CUI '{originalCui}' not found in ANAF registry");
        }

        var entry = foundArray[0];
        var dateGenerale = entry?["date_generale"];
        var companyName = dateGenerale?["denumire"]?.GetValue<string>();

        // "inactpiInactiv" (sic — ANAF's field name) holds the inactive status flag.
        var inactivi = entry?["inactpiInactiv"];
        var statusInactivi = inactivi?["statusInactivi"]?.GetValue<bool>() ?? false;

        return new AnafValidationResult(
            IsValid: true,
            Cui: originalCui,
            CompanyName: companyName,
            IsInactive: statusInactivi,
            ValidationError: null);
    }

    /// <summary>
    /// Strips the "RO" country prefix from a fiscal code if present, and trims surrounding whitespace.
    /// </summary>
    private static string StripCuiPrefix(string cui)
    {
        if (string.IsNullOrWhiteSpace(cui))
            return cui;

        var trimmed = cui.Trim();

        if (trimmed.StartsWith("RO", StringComparison.OrdinalIgnoreCase))
            return trimmed[2..];

        return trimmed;
    }
}
