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

namespace Conspectare.Infrastructure.Llm.Gemini;

/// <summary>
/// HTTP client for the Google Gemini generative language API.
/// Implements <see cref="ILlmApiClient"/> using Gemini's function-calling feature to enforce
/// structured JSON output for both triage (document classification) and data extraction.
/// Supports an optional separate triage model via <see cref="GeminiApiSettings.TriageModel"/>.
/// </summary>
public class GeminiApiClient : ILlmApiClient
{
    // Snake_case serialisation matches the Gemini API wire format.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    private readonly HttpClient _httpClient;
    private readonly GeminiApiSettings _settings;
    private readonly ILogger<GeminiApiClient> _logger;
    private readonly ConspectareMetrics _metrics;

    /// <summary>
    /// Initialises the client and configures the underlying <see cref="HttpClient"/>
    /// with the Gemini base address, timeout, and the API key header.
    /// </summary>
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

    /// <summary>
    /// Sends the document to Gemini and returns a triage classification result.
    /// Uses <see cref="GeminiApiSettings.TriageModel"/> when configured, falling back to
    /// <see cref="GeminiApiSettings.Model"/>. The model is forced to call
    /// <c>classify_document</c> via function calling, ensuring structured output.
    /// </summary>
    public async Task<TriageResult> TriageAsync(
        Document doc, Stream rawFile, string promptText, string promptVersion, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var parts = await BuildPartsAsync(doc, rawFile, ct);

        // Prepend the prompt text so the model sees instructions before the document content.
        parts.Insert(0, new JsonObject
        {
            ["text"] = promptText
        });

        var functionDeclaration = BuildTriageFunctionDeclaration();
        var requestBody = BuildRequestBody(parts, new[] { functionDeclaration }, "classify_document");

        // Null out an empty TriageModel so the model-selection logic treats it as "not set".
        var triageModel = string.IsNullOrEmpty(_settings.TriageModel) ? null : _settings.TriageModel;
        var response = await SendWithRetryAsync(requestBody, ct, triageModel);
        sw.Stop();

        var (args, usage) = ParseFunctionCallResponse(response, "classify_document");

        _metrics.RecordLlmCallDuration("gemini", PipelinePhase.Triage, sw.ElapsedMilliseconds);
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
            // Report whichever model was actually used so callers know which version classified.
            ModelId: triageModel ?? _settings.Model,
            PromptVersion: promptVersion,
            InputTokens: usage.InputTokens,
            OutputTokens: usage.OutputTokens,
            LatencyMs: (int)sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Sends the document to Gemini and returns a structured data extraction result.
    /// The model is forced to call <c>extract_invoice_data</c>, returning all invoice
    /// fields plus any review flags raised by the model.
    /// </summary>
    public async Task<ExtractionResult> ExtractAsync(
        Document doc, Stream rawFile, string documentType, string promptText, string promptVersion, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var parts = await BuildPartsAsync(doc, rawFile, ct);

        // Prepend the prompt text before document content, same as in TriageAsync.
        parts.Insert(0, new JsonObject
        {
            ["text"] = promptText
        });

        var functionDeclaration = BuildExtractionFunctionDeclaration();
        var requestBody = BuildRequestBody(parts, new[] { functionDeclaration }, "extract_invoice_data");
        var response = await SendWithRetryAsync(requestBody, ct);
        sw.Stop();

        var (args, usage) = ParseFunctionCallResponse(response, "extract_invoice_data");

        _metrics.RecordLlmCallDuration("gemini", PipelinePhase.Extraction, sw.ElapsedMilliseconds);
        if (usage.InputTokens.HasValue)
            _metrics.RecordLlmTokens("gemini", "input", usage.InputTokens.Value);
        if (usage.OutputTokens.HasValue)
            _metrics.RecordLlmTokens("gemini", "output", usage.OutputTokens.Value);

        var outputJson = args.ToJsonString(JsonOptions);
        var schemaVersion = "1.0.0";

        // Map the model's review_flags array into typed ReviewFlagInfo records.
        var reviewFlags = new List<ReviewFlagInfo>();
        if (args["review_flags"] is JsonArray flagsArray)
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
    /// Reads the raw file stream and converts it into the appropriate Gemini content part(s).
    /// Images and PDFs are sent as base64-encoded inline data parts; all other content types
    /// are read as plain text parts.
    /// </summary>
    private async Task<List<JsonObject>> BuildPartsAsync(
        Document doc, Stream rawFile, CancellationToken ct)
    {
        var parts = new List<JsonObject>();
        var contentType = doc.ContentType?.ToLowerInvariant() ?? "";

        if (contentType.StartsWith("image/") || contentType == "application/pdf")
        {
            using var ms = new MemoryStream();
            await rawFile.CopyToAsync(ms, ct);
            var base64 = Convert.ToBase64String(ms.ToArray());

            // Fall back to application/octet-stream for any unrecognised binary type.
            var mediaType = contentType switch
            {
                "image/jpeg" => "image/jpeg",
                "image/png" => "image/png",
                "image/gif" => "image/gif",
                "image/webp" => "image/webp",
                "application/pdf" => "application/pdf",
                _ => "application/octet-stream"
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
            // Plain text fallback — leaveOpen so the caller's stream remains usable.
            using var reader = new StreamReader(rawFile, leaveOpen: true);
            var text = await reader.ReadToEndAsync(ct);

            parts.Add(new JsonObject
            {
                ["text"] = text
            });
        }

        return parts;
    }

    /// <summary>
    /// Assembles the Gemini generateContent request body with the provided content parts,
    /// function declarations, and a forced function-calling config so the model must call
    /// exactly the specified function.
    /// </summary>
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
            // "ANY" mode with allowedFunctionNames forces the model to call exactly this function.
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

    /// <summary>
    /// Posts the request body to the Gemini generateContent endpoint, retrying on rate-limit (429)
    /// or service-unavailable (503) responses with an exponential back-off delay.
    /// An optional <paramref name="modelOverride"/> selects a different model for the call
    /// (used by triage when <see cref="GeminiApiSettings.TriageModel"/> is configured).
    /// Throws <see cref="TimeoutException"/> when the HTTP client times out,
    /// and <see cref="HttpRequestException"/> for non-retryable error responses.
    /// </summary>
    internal async Task<JsonObject> SendWithRetryAsync(JsonObject requestBody, CancellationToken ct, string modelOverride = null)
    {
        var maxRetries = _settings.MaxRetries;
        var model = modelOverride ?? _settings.Model;
        var url = $"/v1beta/models/{model}:generateContent";

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
                // TaskCanceledException without a cancelled token means the HttpClient timed out.
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

            // Back-off schedule: 5 s → 15 s → 30 s for subsequent retries.
            var delaySeconds = attempt switch { 0 => 5, 1 => 15, _ => 30 };
            _logger.LogWarning(
                "Gemini API returned {StatusCode}, retrying in {Delay}s (attempt {Attempt}/{MaxRetries})",
                statusCode, delaySeconds, attempt + 1, maxRetries);

            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
        }

        throw new InvalidOperationException("Exhausted all retry attempts");
    }

    /// <summary>
    /// Extracts the function-call arguments for the expected function from the first
    /// candidate in the Gemini response, along with token usage metadata.
    /// Throws <see cref="InvalidOperationException"/> if the response structure is unexpected
    /// or the target function call is absent.
    /// </summary>
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

        // Gemini uses "usageMetadata" with different field names from the OpenAI/Claude convention.
        var usageMetadata = response["usageMetadata"]?.AsObject();
        var inputTokens = usageMetadata?["promptTokenCount"]?.GetValue<int>();
        var outputTokens = usageMetadata?["candidatesTokenCount"]?.GetValue<int>();

        return (functionArgs, new UsageInfo(inputTokens, outputTokens));
    }

    /// <summary>
    /// Builds the Gemini function declaration for <c>classify_document</c>,
    /// which forces the model to output document type, confidence score, and accounting relevance.
    /// </summary>
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

    /// <summary>
    /// Builds the Gemini function declaration for <c>extract_invoice_data</c>,
    /// covering the full Romanian invoice schema with rich per-field descriptions
    /// (supplier, customer, line items with VAT, totals, payment details, and review flags).
    /// A local helper delegate is used to avoid duplicating the shared party-properties schema.
    /// </summary>
    private static JsonObject BuildExtractionFunctionDeclaration()
    {
        // Builds shared party properties (supplier or customer). includeBank adds IBAN/bank fields.
        var partyProperties = new Func<bool, JsonObject>(includeBank =>
        {
            var props = new JsonObject
            {
                ["name"] = new JsonObject { ["type"] = "string", ["description"] = "Denumire completă (conform act constitutiv)" },
                ["tax_id"] = new JsonObject { ["type"] = "string", ["description"] = "CUI/CIF, cu sau fără prefix RO" },
                ["vat_registered"] = new JsonObject { ["type"] = "boolean", ["description"] = "Plătitor de TVA (true dacă are prefix RO la CUI)" },
                ["trade_register_number"] = new JsonObject { ["type"] = "string", ["description"] = "Nr. Reg. Com. (ex: J40/1234/2020 sau F40/1234/2020)" },
                ["address"] = new JsonObject { ["type"] = "string", ["description"] = "Adresa completă (stradă, nr, bloc, scara, etaj, apartament)" },
                ["city"] = new JsonObject { ["type"] = "string", ["description"] = "Localitate / Oraș" },
                ["county"] = new JsonObject { ["type"] = "string", ["description"] = "Județ" },
                ["country_code"] = new JsonObject { ["type"] = "string", ["description"] = "ISO 3166-1 alpha-2 (default RO)" },
                ["phone"] = new JsonObject { ["type"] = "string", ["description"] = "Telefon" },
                ["email"] = new JsonObject { ["type"] = "string", ["description"] = "Email" }
            };

            if (includeBank)
            {
                props["bank_account"] = new JsonObject { ["type"] = "string", ["description"] = "IBAN" };
                props["bank_name"] = new JsonObject { ["type"] = "string", ["description"] = "Denumire bancă" };
                props["swift_bic"] = new JsonObject { ["type"] = "string", ["description"] = "SWIFT/BIC code" };
            }

            return props;
        });

        return new JsonObject
        {
            ["name"] = "extract_invoice_data",
            ["description"] = "Extract all structured accounting data from a Romanian invoice/receipt into the canonical schema. Be exhaustive — extract every field present in the document.",
            ["parameters"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["supplier"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["description"] = "Furnizor / Emitent",
                        ["properties"] = partyProperties(true)
                    },
                    ["customer"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["description"] = "Client / Cumpărător / Beneficiar",
                        ["properties"] = partyProperties(true)
                    },
                    ["invoice_number"] = new JsonObject { ["type"] = "string", ["description"] = "Număr factură complet cu seria (ex: ABC 0001234)" },
                    ["invoice_series"] = new JsonObject { ["type"] = "string", ["description"] = "Seria facturii separat (ex: ABC, DF-B, FCT)" },
                    ["invoice_date"] = new JsonObject { ["type"] = "string", ["description"] = "Data emiterii — ISO 8601 (YYYY-MM-DD)" },
                    ["due_date"] = new JsonObject { ["type"] = "string", ["description"] = "Data scadentă — ISO 8601" },
                    ["delivery_date"] = new JsonObject { ["type"] = "string", ["description"] = "Data livrării / prestării — ISO 8601" },
                    ["currency"] = new JsonObject { ["type"] = "string", ["description"] = "ISO 4217 (RON, EUR, USD)" },
                    ["exchange_rate"] = new JsonObject { ["type"] = "number", ["description"] = "Curs valutar BNR la data facturii (dacă moneda ≠ RON)" },
                    ["document_type"] = new JsonObject { ["type"] = "string", ["description"] = "Tip document standardizat: invoice, receipt, unknown" },
                    ["raw_document_type"] = new JsonObject { ["type"] = "string", ["description"] = "Tipul documentului exact cum apare scris pe document (ex: FACTURĂ FISCALĂ, TAX INVOICE, BON FISCAL, FACTURA PROFORMA)" },
                    ["document_language"] = new JsonObject { ["type"] = "string", ["description"] = "Limba documentului — cod ISO 639-1 de 2 litere (ro, en, fr, de, it, es, etc.)" },
                    ["line_items"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["description"] = "Produse / Servicii / Linii factură",
                        ["items"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["line_number"] = new JsonObject { ["type"] = "integer", ["description"] = "Nr. crt." },
                                ["product_code"] = new JsonObject { ["type"] = "string", ["description"] = "Cod produs / SKU" },
                                ["description"] = new JsonObject { ["type"] = "string", ["description"] = "Descriere produs/serviciu" },
                                ["quantity"] = new JsonObject { ["type"] = "number", ["description"] = "Cantitate" },
                                ["unit"] = new JsonObject { ["type"] = "string", ["description"] = "U.M. (buc, kg, ore, m², l, km, zi, luna)" },
                                ["unit_price"] = new JsonObject { ["type"] = "number", ["description"] = "Preț unitar FĂRĂ TVA" },
                                ["unit_price_with_vat"] = new JsonObject { ["type"] = "number", ["description"] = "Preț unitar CU TVA (dacă facturat incluzând TVA)" },
                                ["discount_percent"] = new JsonObject { ["type"] = "number", ["description"] = "Discount % pe linie" },
                                ["discount_amount"] = new JsonObject { ["type"] = "number", ["description"] = "Valoare discount pe linie" },
                                ["line_total_without_vat"] = new JsonObject { ["type"] = "number", ["description"] = "Valoare linie FĂRĂ TVA" },
                                ["vat_rate"] = new JsonObject { ["type"] = "number", ["description"] = "Cotă TVA — copy EXACT percentage from document, do NOT assume standard rates" },
                                ["vat_category"] = new JsonObject { ["type"] = "string", ["description"] = "Categorie TVA: S (standard), AE (scutit cu drept de deducere), E (scutit fără drept), Z (cotă zero), O (neimpozabil)" },
                                ["vat_amount"] = new JsonObject { ["type"] = "number", ["description"] = "TVA pe linie" },
                                ["line_total"] = new JsonObject { ["type"] = "number", ["description"] = "Total linie CU TVA" }
                            }
                        }
                    },
                    ["discount"] = new JsonObject { ["type"] = "number", ["description"] = "Discount global (valoare)" },
                    ["discount_percent"] = new JsonObject { ["type"] = "number", ["description"] = "Discount global %" },
                    ["tax_exclusive_amount"] = new JsonObject { ["type"] = "number", ["description"] = "Total FĂRĂ TVA (baza impozabilă) — copy EXACT printed value, do NOT calculate" },
                    ["total_vat"] = new JsonObject { ["type"] = "number", ["description"] = "Total TVA — copy EXACT printed value, do NOT calculate as percentage of subtotal" },
                    ["tax_inclusive_amount"] = new JsonObject { ["type"] = "number", ["description"] = "Total CU TVA (total de plată) — copy EXACT printed value, do NOT calculate" },
                    ["vat_breakdown"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["description"] = "Detaliere TVA pe cote",
                        ["items"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["vat_rate"] = new JsonObject { ["type"] = "number" },
                                ["vat_category"] = new JsonObject { ["type"] = "string" },
                                ["taxable_amount"] = new JsonObject { ["type"] = "number", ["description"] = "Baza impozabilă pentru cota respectivă" },
                                ["vat_amount"] = new JsonObject { ["type"] = "number", ["description"] = "TVA calculat" }
                            }
                        }
                    },
                    ["payment_method"] = new JsonObject { ["type"] = "string", ["description"] = "Modalitate plată: transfer_bancar, numerar, card, cec, bilet_la_ordin, compensare" },
                    ["payment_reference"] = new JsonObject { ["type"] = "string", ["description"] = "Referință plată (nr. OP, nr. chitanță)" },
                    ["payment_terms"] = new JsonObject { ["type"] = "string", ["description"] = "Condiții de plată (ex: 30 zile de la emitere)" },
                    ["contract_reference"] = new JsonObject { ["type"] = "string", ["description"] = "Nr. contract / comandă" },
                    ["delivery_note_number"] = new JsonObject { ["type"] = "string", ["description"] = "Nr. aviz de însoțire" },
                    ["transport_details"] = new JsonObject { ["type"] = "string", ["description"] = "Detalii transport (nr. auto, delegat)" },
                    ["stamp_duty"] = new JsonObject { ["type"] = "number", ["description"] = "Taxă de timbru (dacă există)" },
                    ["penalties"] = new JsonObject { ["type"] = "number", ["description"] = "Penalități / majorări de întârziere" },
                    ["tax_note"] = new JsonObject { ["type"] = "string", ["description"] = "Mențiuni fiscale (scutire TVA, taxare inversă, regim special, etc.)" },
                    ["notes"] = new JsonObject { ["type"] = "string", ["description"] = "Observații, mențiuni suplimentare" },
                    ["is_reverse_charge"] = new JsonObject { ["type"] = "boolean", ["description"] = "Taxare inversă (TVA datorat de beneficiar)" },
                    ["is_self_billing"] = new JsonObject { ["type"] = "boolean", ["description"] = "Autofacturare" },
                    ["credit_note_reference"] = new JsonObject { ["type"] = "string", ["description"] = "Nr. factură originală (dacă e notă de credit/storno)" },
                    ["review_flags"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["description"] = "Semnalizări calitate date — generează pentru orice problemă detectată",
                        ["items"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["flag_type"] = new JsonObject { ["type"] = "string", ["description"] = "missing_field, calculation_mismatch, format_anomaly, low_confidence, duplicate_suspicion" },
                                ["severity"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("info", "warning", "error") },
                                ["message"] = new JsonObject { ["type"] = "string" },
                                ["field_path"] = new JsonObject { ["type"] = "string", ["description"] = "Câmpul la care se referă (ex: supplier.tax_id, line_items[0].vat_rate)" }
                            }
                        }
                    }
                },
                ["required"] = new JsonArray("invoice_number", "invoice_date", "currency", "line_items", "tax_exclusive_amount", "total_vat", "tax_inclusive_amount", "supplier", "customer")
            }
        };
    }

    /// <summary>Token usage counters returned by the Gemini API usageMetadata object.</summary>
    internal record UsageInfo(int? InputTokens, int? OutputTokens);
}
