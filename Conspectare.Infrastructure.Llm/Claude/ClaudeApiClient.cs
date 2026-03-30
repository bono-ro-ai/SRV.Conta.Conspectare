using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Conspectare.Services.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Conspectare.Infrastructure.Llm.Claude;

/// <summary>
/// HTTP client for the Anthropic Claude Messages API.
/// Implements <see cref="ILlmApiClient"/> using Claude's tool-use feature to enforce
/// structured JSON output for both triage (document classification) and data extraction.
/// </summary>
public class ClaudeApiClient : ILlmApiClient
{
    // The anthropic-version header value required by all Claude API requests.
    private const string AnthropicVersion = "2023-06-01";

    // Snake_case serialisation matches the Claude API wire format.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    private readonly HttpClient _httpClient;
    private readonly ClaudeApiSettings _settings;
    private readonly ILogger<ClaudeApiClient> _logger;
    private readonly ConspectareMetrics _metrics;

    /// <summary>
    /// Initialises the client and configures the underlying <see cref="HttpClient"/>
    /// with the base address, timeout, and required Anthropic authentication headers.
    /// </summary>
    public ClaudeApiClient(
        HttpClient httpClient,
        IOptions<ClaudeApiSettings> options,
        ILogger<ClaudeApiClient> logger,
        ConspectareMetrics metrics)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
        _metrics = metrics;

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _settings.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
    }

    /// <summary>
    /// Sends the document to Claude and returns a triage classification result.
    /// The model is forced to call the <c>classify_document</c> tool, ensuring
    /// a structured JSON response with document type, confidence, and relevance flag.
    /// </summary>
    public async Task<TriageResult> TriageAsync(
        Document doc, Stream rawFile, string promptText, string promptVersion, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var contentBlocks = await BuildContentBlocksAsync(doc, rawFile, ct);

        // Prepend the prompt text as the first content block so the model sees
        // instructions before the document content.
        contentBlocks.Insert(0, new JsonObject
        {
            ["type"] = "text",
            ["text"] = promptText
        });

        var tool = BuildTriageTool();
        var requestBody = BuildRequestBody(contentBlocks, new[] { tool }, "classify_document");
        var response = await SendWithRetryAsync(requestBody, ct);
        sw.Stop();

        var (toolInput, usage) = ParseToolUseResponse(response, "classify_document");

        _metrics.RecordLlmCallDuration("claude", PipelinePhase.Triage, sw.ElapsedMilliseconds);
        if (usage.InputTokens.HasValue)
            _metrics.RecordLlmTokens("claude", "input", usage.InputTokens.Value);
        if (usage.OutputTokens.HasValue)
            _metrics.RecordLlmTokens("claude", "output", usage.OutputTokens.Value);

        var documentType = toolInput["document_type"]?.GetValue<string>() ?? "unknown";
        var confidence = toolInput["confidence"]?.GetValue<decimal>() ?? 0m;
        var isAccountingRelevant = toolInput["is_accounting_relevant"]?.GetValue<bool>() ?? false;

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

    /// <summary>
    /// Sends the document to Claude and returns a structured data extraction result.
    /// The model is forced to call the <c>extract_invoice_data</c> tool, returning
    /// all invoice fields plus any review flags raised by the model.
    /// </summary>
    public async Task<ExtractionResult> ExtractAsync(
        Document doc, Stream rawFile, string documentType, string promptText, string promptVersion, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var contentBlocks = await BuildContentBlocksAsync(doc, rawFile, ct);

        // Prepend the prompt text before document content, same as in TriageAsync.
        contentBlocks.Insert(0, new JsonObject
        {
            ["type"] = "text",
            ["text"] = promptText
        });

        var tool = BuildExtractionTool();
        var requestBody = BuildRequestBody(contentBlocks, new[] { tool }, "extract_invoice_data");
        var response = await SendWithRetryAsync(requestBody, ct);
        sw.Stop();

        var (toolInput, usage) = ParseToolUseResponse(response, "extract_invoice_data");

        _metrics.RecordLlmCallDuration("claude", PipelinePhase.Extraction, sw.ElapsedMilliseconds);
        if (usage.InputTokens.HasValue)
            _metrics.RecordLlmTokens("claude", "input", usage.InputTokens.Value);
        if (usage.OutputTokens.HasValue)
            _metrics.RecordLlmTokens("claude", "output", usage.OutputTokens.Value);

        var outputJson = toolInput.ToJsonString(JsonOptions);
        var schemaVersion = "1.0.0";

        // Map the model's review_flags array into typed ReviewFlagInfo records.
        var reviewFlags = new List<ReviewFlagInfo>();
        if (toolInput["review_flags"] is JsonArray flagsArray)
        {
            foreach (var flag in flagsArray)
            {
                if (flag is JsonObject flagObj)
                {
                    reviewFlags.Add(new ReviewFlagInfo(
                        FlagType: flagObj["flag_type"]?.GetValue<string>() ?? "unknown",
                        Severity: flagObj["severity"]?.GetValue<string>() ?? ReviewFlagSeverity.Info,
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

    /// <summary>
    /// Reads the raw file stream and converts it into the appropriate Claude content block(s).
    /// PDFs are sent as base64-encoded document blocks; images as base64 image blocks;
    /// all other content types are read as plain text blocks.
    /// </summary>
    private async Task<List<JsonObject>> BuildContentBlocksAsync(
        Document doc, Stream rawFile, CancellationToken ct)
    {
        var blocks = new List<JsonObject>();
        var contentType = doc.ContentType?.ToLowerInvariant() ?? "";

        if (contentType == "application/pdf")
        {
            using var ms = new MemoryStream();
            await rawFile.CopyToAsync(ms, ct);
            var base64 = Convert.ToBase64String(ms.ToArray());

            blocks.Add(new JsonObject
            {
                ["type"] = "document",
                ["source"] = new JsonObject
                {
                    ["type"] = "base64",
                    ["media_type"] = "application/pdf",
                    ["data"] = base64
                }
            });
        }
        else if (contentType.StartsWith("image/"))
        {
            using var ms = new MemoryStream();
            await rawFile.CopyToAsync(ms, ct);
            var base64 = Convert.ToBase64String(ms.ToArray());

            // Fall back to image/jpeg for any unrecognised image sub-type.
            var mediaType = contentType switch
            {
                "image/jpeg" => "image/jpeg",
                "image/png" => "image/png",
                "image/gif" => "image/gif",
                "image/webp" => "image/webp",
                _ => "image/jpeg"
            };

            blocks.Add(new JsonObject
            {
                ["type"] = "image",
                ["source"] = new JsonObject
                {
                    ["type"] = "base64",
                    ["media_type"] = mediaType,
                    ["data"] = base64
                }
            });
        }
        else
        {
            // Plain text fallback — leaveOpen so the caller's stream remains usable.
            using var reader = new StreamReader(rawFile, leaveOpen: true);
            var text = await reader.ReadToEndAsync(ct);

            blocks.Add(new JsonObject
            {
                ["type"] = "text",
                ["text"] = text
            });
        }

        return blocks;
    }

    /// <summary>
    /// Assembles the Claude Messages API request body with the provided content blocks,
    /// tool definitions, and a forced tool choice so the model must call the specified tool.
    /// </summary>
    private JsonObject BuildRequestBody(
        List<JsonObject> contentBlocks, JsonObject[] tools, string forcedToolName)
    {
        var messagesArray = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "user",
                // Wrap the content blocks into a JsonArray of JsonNode to satisfy the API shape.
                ["content"] = new JsonArray(contentBlocks.Select(b => (JsonNode)b).ToArray())
            }
        };

        var toolsArray = new JsonArray(tools.Select(t => (JsonNode)t).ToArray());

        return new JsonObject
        {
            ["model"] = _settings.Model,
            ["max_tokens"] = _settings.MaxTokens,
            ["messages"] = messagesArray,
            ["tools"] = toolsArray,
            // "tool" mode with an explicit name forces the model to call exactly that tool.
            ["tool_choice"] = new JsonObject
            {
                ["type"] = "tool",
                ["name"] = forcedToolName
            }
        };
    }

    /// <summary>
    /// Posts the request body to the Claude Messages endpoint, retrying on rate-limit (429)
    /// or service-unavailable (503) responses with an exponential back-off delay.
    /// Throws <see cref="TimeoutException"/> when the HTTP client times out,
    /// and <see cref="HttpRequestException"/> for non-retryable error responses.
    /// </summary>
    internal async Task<JsonObject> SendWithRetryAsync(JsonObject requestBody, CancellationToken ct)
    {
        var maxRetries = _settings.MaxRetries;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            using var content = new StringContent(
                requestBody.ToJsonString(JsonOptions),
                Encoding.UTF8,
                new MediaTypeHeaderValue("application/json"));

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync("/v1/messages", content, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // TaskCanceledException without a cancelled token means the HttpClient timed out.
                throw new TimeoutException(
                    $"Claude API request timed out after {_settings.TimeoutSeconds} seconds");
            }

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync(ct);
                return JsonNode.Parse(responseJson)?.AsObject()
                       ?? throw new InvalidOperationException("Claude API returned empty response");
            }

            var statusCode = (int)response.StatusCode;
            var isRetryable = statusCode == 429 || statusCode == 503;

            if (!isRetryable || attempt >= maxRetries)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException(
                    $"Claude API returned {statusCode}: {errorBody}",
                    null,
                    response.StatusCode);
            }

            // Back-off schedule: 5 s → 15 s → 30 s for subsequent retries.
            var delaySeconds = attempt switch { 0 => 5, 1 => 15, _ => 30 };
            _logger.LogWarning(
                "Claude API returned {StatusCode}, retrying in {Delay}s (attempt {Attempt}/{MaxRetries})",
                statusCode, delaySeconds, attempt + 1, maxRetries);

            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
        }

        throw new InvalidOperationException("Exhausted all retry attempts");
    }

    /// <summary>
    /// Locates the <c>tool_use</c> block with the given tool name in the API response content array
    /// and returns its parsed input object alongside token usage metadata.
    /// Throws <see cref="InvalidOperationException"/> if the expected block is absent.
    /// </summary>
    private static (JsonObject ToolInput, UsageInfo Usage) ParseToolUseResponse(
        JsonObject response, string expectedToolName)
    {
        var contentArray = response["content"]?.AsArray()
            ?? throw new InvalidOperationException("Claude API response missing 'content' array");

        JsonObject toolInput = null;
        foreach (var block in contentArray)
        {
            if (block?["type"]?.GetValue<string>() == "tool_use" &&
                block["name"]?.GetValue<string>() == expectedToolName)
            {
                toolInput = block["input"]?.AsObject();
                break;
            }
        }

        if (toolInput == null)
        {
            throw new InvalidOperationException(
                $"Claude API response did not contain expected tool_use block '{expectedToolName}'");
        }

        var usage = response["usage"]?.AsObject();
        var inputTokens = usage?["input_tokens"]?.GetValue<int>();
        var outputTokens = usage?["output_tokens"]?.GetValue<int>();

        return (toolInput, new UsageInfo(inputTokens, outputTokens));
    }

    /// <summary>
    /// Builds the Claude tool definition for the <c>classify_document</c> function,
    /// which forces the model to output document type, confidence score, and accounting relevance.
    /// </summary>
    private static JsonObject BuildTriageTool()
    {
        return new JsonObject
        {
            ["name"] = "classify_document",
            ["description"] = "Classify the document type and determine if it is accounting-relevant",
            ["input_schema"] = new JsonObject
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

    /// <summary>
    /// Builds the Claude tool definition for the <c>extract_invoice_data</c> function,
    /// covering the full canonical invoice schema (supplier, customer, line items, totals,
    /// VAT, payment details) plus a <c>review_flags</c> array for data quality signals.
    /// </summary>
    private static JsonObject BuildExtractionTool()
    {
        return new JsonObject
        {
            ["name"] = "extract_invoice_data",
            ["description"] = "Extract structured accounting data from the document",
            ["input_schema"] = new JsonObject
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
                    ["subtotal"] = new JsonObject { ["type"] = "number", ["description"] = "Total FĂRĂ TVA — copy EXACT printed value, do NOT calculate" },
                    ["total_vat"] = new JsonObject { ["type"] = "number", ["description"] = "Total TVA — copy EXACT printed value, do NOT calculate as percentage of subtotal" },
                    ["total"] = new JsonObject { ["type"] = "number", ["description"] = "Total CU TVA — copy EXACT printed value, do NOT calculate" },
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

    /// <summary>Token usage counters returned by the Claude API usage object.</summary>
    internal record UsageInfo(int? InputTokens, int? OutputTokens);
}
