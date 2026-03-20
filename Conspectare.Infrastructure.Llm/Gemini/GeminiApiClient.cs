using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Conspectare.Domain.Entities;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Conspectare.Services.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Conspectare.Infrastructure.Llm.Gemini;
public class GeminiApiClient : ILlmApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };
    private readonly HttpClient _httpClient;
    private readonly GeminiApiSettings _settings;
    private readonly ILogger<GeminiApiClient> _logger;
    private readonly ConspectareMetrics _metrics;
    public GeminiApiClient(
        HttpClient httpClient,
        IOptions<GeminiApiSettings> options,
        ILogger<GeminiApiClient> logger,
        ConspectareMetrics metrics)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
        _metrics = metrics;
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", _settings.ApiKey);
    }
    public async Task<TriageResult> TriageAsync(
        Document doc, Stream rawFile, string promptText, string promptVersion, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var parts = await BuildPartsAsync(doc, rawFile, ct);
        parts.Insert(0, new JsonObject
        {
            ["text"] = promptText
        });
        var functionDeclaration = BuildTriageFunctionDeclaration();
        var requestBody = BuildRequestBody(parts, new[] { functionDeclaration }, "classify_document");
        var response = await SendWithRetryAsync(requestBody, ct);
        sw.Stop();
        var (args, usage) = ParseFunctionCallResponse(response, "classify_document");
        _metrics.RecordLlmCallDuration("gemini", "triage", sw.ElapsedMilliseconds);
        if (usage.InputTokens.HasValue)
            _metrics.RecordLlmTokens("gemini", "input", usage.InputTokens.Value);
        if (usage.OutputTokens.HasValue)
            _metrics.RecordLlmTokens("gemini", "output", usage.OutputTokens.Value);
        var documentType = args["document_type"]?.GetValue<string>() ?? "unknown";
        var confidence = args["confidence"]?.GetValue<decimal>() ?? 0m;
        var isAccountingRelevant = args["is_accounting_relevant"]?.GetValue<bool>() ?? false;
        return new TriageResult(
            DocumentType: documentType,
            Confidence: confidence,
            IsAccountingRelevant: isAccountingRelevant,
            ModelId: _settings.Model,
            PromptVersion: promptVersion,
            InputTokens: usage.InputTokens,
            OutputTokens: usage.OutputTokens,
            LatencyMs: (int)sw.ElapsedMilliseconds);
    }
    public async Task<ExtractionResult> ExtractAsync(
        Document doc, Stream rawFile, string documentType, string promptText, string promptVersion, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var parts = await BuildPartsAsync(doc, rawFile, ct);
        parts.Insert(0, new JsonObject
        {
            ["text"] = promptText
        });
        var functionDeclaration = BuildExtractionFunctionDeclaration();
        var requestBody = BuildRequestBody(parts, new[] { functionDeclaration }, "extract_invoice_data");
        var response = await SendWithRetryAsync(requestBody, ct);
        sw.Stop();
        var (args, usage) = ParseFunctionCallResponse(response, "extract_invoice_data");
        _metrics.RecordLlmCallDuration("gemini", "extraction", sw.ElapsedMilliseconds);
        if (usage.InputTokens.HasValue)
            _metrics.RecordLlmTokens("gemini", "input", usage.InputTokens.Value);
        if (usage.OutputTokens.HasValue)
            _metrics.RecordLlmTokens("gemini", "output", usage.OutputTokens.Value);
        var outputJson = args.ToJsonString(JsonOptions);
        var schemaVersion = "1.0.0";
        var reviewFlags = new List<ReviewFlagInfo>();
        if (args["review_flags"] is JsonArray flagsArray)
        {
            foreach (var flag in flagsArray)
            {
                if (flag is JsonObject flagObj)
                {
                    reviewFlags.Add(new ReviewFlagInfo(
                        FlagType: flagObj["flag_type"]?.GetValue<string>() ?? "unknown",
                        Severity: flagObj["severity"]?.GetValue<string>() ?? "info",
                        Message: flagObj["message"]?.GetValue<string>() ?? ""));
                }
            }
        }
        return new ExtractionResult(
            OutputJson: outputJson,
            SchemaVersion: schemaVersion,
            ModelId: _settings.Model,
            PromptVersion: promptVersion,
            InputTokens: usage.InputTokens,
            OutputTokens: usage.OutputTokens,
            LatencyMs: (int)sw.ElapsedMilliseconds,
            ReviewFlags: reviewFlags);
    }
    private async Task<List<JsonObject>> BuildPartsAsync(
        Document doc, Stream rawFile, CancellationToken ct)
    {
        var parts = new List<JsonObject>();
        var contentType = doc.ContentType?.ToLowerInvariant() ?? "";
        if (contentType.StartsWith("image/"))
        {
            using var ms = new MemoryStream();
            await rawFile.CopyToAsync(ms, ct);
            var base64 = Convert.ToBase64String(ms.ToArray());
            var mediaType = contentType switch
            {
                "image/jpeg" => "image/jpeg",
                "image/png" => "image/png",
                "image/gif" => "image/gif",
                "image/webp" => "image/webp",
                _ => "image/jpeg"
            };
            parts.Add(new JsonObject
            {
                ["inlineData"] = new JsonObject
                {
                    ["mimeType"] = mediaType,
                    ["data"] = base64
                }
            });
        }
        else
        {
            using var reader = new StreamReader(rawFile, leaveOpen: true);
            var text = await reader.ReadToEndAsync(ct);
            parts.Add(new JsonObject
            {
                ["text"] = text
            });
        }
        return parts;
    }
    private JsonObject BuildRequestBody(
        List<JsonObject> parts, JsonObject[] functionDeclarations, string forcedFunctionName)
    {
        var contentsArray = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(parts.Select(p => (JsonNode)p).ToArray())
            }
        };
        var declarationsArray = new JsonArray(functionDeclarations.Select(d => (JsonNode)d).ToArray());
        return new JsonObject
        {
            ["contents"] = contentsArray,
            ["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["functionDeclarations"] = declarationsArray
                }
            },
            ["toolConfig"] = new JsonObject
            {
                ["functionCallingConfig"] = new JsonObject
                {
                    ["mode"] = "ANY",
                    ["allowedFunctionNames"] = new JsonArray(forcedFunctionName)
                }
            },
            ["generationConfig"] = new JsonObject
            {
                ["maxOutputTokens"] = _settings.MaxOutputTokens
            }
        };
    }
    internal async Task<JsonObject> SendWithRetryAsync(JsonObject requestBody, CancellationToken ct)
    {
        var maxRetries = _settings.MaxRetries;
        var url = $"/v1beta/models/{_settings.Model}:generateContent";
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            using var content = new StringContent(
                requestBody.ToJsonString(JsonOptions),
                Encoding.UTF8,
                new MediaTypeHeaderValue("application/json"));
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync(url, content, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Gemini API request timed out after {_settings.TimeoutSeconds} seconds");
            }
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync(ct);
                return JsonNode.Parse(responseJson)?.AsObject()
                       ?? throw new InvalidOperationException("Gemini API returned empty response");
            }
            var statusCode = (int)response.StatusCode;
            var isRetryable = statusCode == 429 || statusCode == 503;
            if (!isRetryable || attempt >= maxRetries)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException(
                    $"Gemini API returned {statusCode}: {errorBody}",
                    null,
                    response.StatusCode);
            }
            var delaySeconds = attempt switch { 0 => 5, 1 => 15, _ => 30 };
            _logger.LogWarning(
                "Gemini API returned {StatusCode}, retrying in {Delay}s (attempt {Attempt}/{MaxRetries})",
                statusCode, delaySeconds, attempt + 1, maxRetries);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
        }
        throw new InvalidOperationException("Exhausted all retry attempts");
    }
    private static (JsonObject Args, UsageInfo Usage) ParseFunctionCallResponse(
        JsonObject response, string expectedFunctionName)
    {
        var candidates = response["candidates"]?.AsArray();
        if (candidates == null || candidates.Count == 0)
        {
            throw new InvalidOperationException("Gemini API response missing or empty 'candidates' array");
        }
        var partsArray = candidates[0]?["content"]?["parts"]?.AsArray()
            ?? throw new InvalidOperationException("Gemini API response missing 'parts' array");
        JsonObject functionArgs = null;
        foreach (var part in partsArray)
        {
            var functionCall = part?["functionCall"];
            if (functionCall != null && functionCall["name"]?.GetValue<string>() == expectedFunctionName)
            {
                functionArgs = functionCall["args"]?.AsObject();
                break;
            }
        }
        if (functionArgs == null)
        {
            throw new InvalidOperationException(
                $"Gemini API response did not contain expected functionCall '{expectedFunctionName}'");
        }
        var usageMetadata = response["usageMetadata"]?.AsObject();
        var inputTokens = usageMetadata?["promptTokenCount"]?.GetValue<int>();
        var outputTokens = usageMetadata?["candidatesTokenCount"]?.GetValue<int>();
        return (functionArgs, new UsageInfo(inputTokens, outputTokens));
    }
    private static JsonObject BuildTriageFunctionDeclaration()
    {
        return new JsonObject
        {
            ["name"] = "classify_document",
            ["description"] = "Classify the document type and determine if it is accounting-relevant",
            ["parameters"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["document_type"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JsonArray("invoice", "receipt", "proforma", "non_accounting", "unknown")
                    },
                    ["confidence"] = new JsonObject
                    {
                        ["type"] = "number",
                        ["minimum"] = 0,
                        ["maximum"] = 1
                    },
                    ["is_accounting_relevant"] = new JsonObject
                    {
                        ["type"] = "boolean"
                    },
                    ["reasoning"] = new JsonObject
                    {
                        ["type"] = "string"
                    }
                },
                ["required"] = new JsonArray("document_type", "confidence", "is_accounting_relevant")
            }
        };
    }
    private static JsonObject BuildExtractionFunctionDeclaration()
    {
        return new JsonObject
        {
            ["name"] = "extract_invoice_data",
            ["description"] = "Extract structured accounting data from the document",
            ["parameters"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["supplier"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["name"] = new JsonObject { ["type"] = "string" },
                            ["tax_id"] = new JsonObject { ["type"] = "string" },
                            ["trade_register_number"] = new JsonObject { ["type"] = "string" },
                            ["address"] = new JsonObject { ["type"] = "string" },
                            ["city"] = new JsonObject { ["type"] = "string" },
                            ["county"] = new JsonObject { ["type"] = "string" },
                            ["country_code"] = new JsonObject { ["type"] = "string" },
                            ["bank_account"] = new JsonObject { ["type"] = "string" },
                            ["bank_name"] = new JsonObject { ["type"] = "string" }
                        }
                    },
                    ["customer"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["name"] = new JsonObject { ["type"] = "string" },
                            ["tax_id"] = new JsonObject { ["type"] = "string" },
                            ["trade_register_number"] = new JsonObject { ["type"] = "string" },
                            ["address"] = new JsonObject { ["type"] = "string" },
                            ["city"] = new JsonObject { ["type"] = "string" },
                            ["county"] = new JsonObject { ["type"] = "string" },
                            ["country_code"] = new JsonObject { ["type"] = "string" }
                        }
                    },
                    ["invoice_number"] = new JsonObject { ["type"] = "string" },
                    ["invoice_date"] = new JsonObject { ["type"] = "string", ["description"] = "ISO 8601 date" },
                    ["due_date"] = new JsonObject { ["type"] = "string", ["description"] = "ISO 8601 date" },
                    ["currency"] = new JsonObject { ["type"] = "string" },
                    ["line_items"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["items"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["description"] = new JsonObject { ["type"] = "string" },
                                ["quantity"] = new JsonObject { ["type"] = "number" },
                                ["unit"] = new JsonObject { ["type"] = "string" },
                                ["unit_price"] = new JsonObject { ["type"] = "number" },
                                ["vat_rate"] = new JsonObject { ["type"] = "number" },
                                ["vat_amount"] = new JsonObject { ["type"] = "number" },
                                ["line_total"] = new JsonObject { ["type"] = "number" }
                            }
                        }
                    },
                    ["subtotal"] = new JsonObject { ["type"] = "number" },
                    ["total_vat"] = new JsonObject { ["type"] = "number" },
                    ["total"] = new JsonObject { ["type"] = "number" },
                    ["payment_method"] = new JsonObject { ["type"] = "string" },
                    ["notes"] = new JsonObject { ["type"] = "string" },
                    ["review_flags"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["items"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["flag_type"] = new JsonObject { ["type"] = "string" },
                                ["severity"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("info", "warning", "error") },
                                ["message"] = new JsonObject { ["type"] = "string" }
                            }
                        }
                    }
                },
                ["required"] = new JsonArray("invoice_number", "invoice_date", "currency", "line_items", "total")
            }
        };
    }
    internal record UsageInfo(int? InputTokens, int? OutputTokens);
}
