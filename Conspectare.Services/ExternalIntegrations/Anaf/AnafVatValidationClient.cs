using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Conspectare.Services.ExternalIntegrations.Anaf;

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

        if (numericCui.Length < 2 || numericCui.Length > 10)
        {
            return new AnafValidationResult(
                IsValid: false,
                Cui: cui,
                CompanyName: null,
                IsInactive: false,
                ValidationError: $"CUI '{cui}' has invalid length ({numericCui.Length} digits) — Romanian CUIs must be 2-10 digits");
        }

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
            catch (HttpRequestException ex)
            {
                return new AnafValidationResult(
                    IsValid: false,
                    Cui: cui,
                    CompanyName: null,
                    IsInactive: false,
                    ValidationError: "ANAF API request failed");
            }

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

    internal async Task<AnafValidationResult> SendRequestAsync(
        string originalCui, JsonArray requestBody, CancellationToken ct)
    {
        using var content = new StringContent(
            requestBody.ToJsonString(),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("/api/v8/ws/tva", content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        return ParseResponse(originalCui, responseJson);
    }

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
        var inactivi = entry?["inactpiInactiv"];
        var statusInactivi = inactivi?["statusInactivi"]?.GetValue<bool>() ?? false;

        return new AnafValidationResult(
            IsValid: true,
            Cui: originalCui,
            CompanyName: companyName,
            IsInactive: statusInactivi,
            ValidationError: null);
    }

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
