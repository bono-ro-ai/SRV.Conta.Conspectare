using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Conspectare.Domain.Entities;
using Conspectare.Infrastructure.Llm.Claude;
using Conspectare.Services.Observability;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Conspectare.Tests;

public class ClaudeApiClientTests
{
    private static ClaudeApiSettings DefaultSettings => new()
    {
        ApiKey = "test-key",
        Model = "claude-sonnet-4-20250514",
        MaxTokens = 4096,
        BaseUrl = "https://api.anthropic.com",
        TimeoutSeconds = 30,
        MaxRetries = 3
    };

    private static Document CreateTestDocument(string contentType = "image/jpeg") => new()
    {
        Id = 1,
        TenantId = 1,
        FileName = "test.jpg",
        ContentType = contentType,
        FileSizeBytes = 1024,
        InputFormat = "image",
        Status = "triaging",
        RawFileS3Key = "tenant-1/raw/test.jpg",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Stream CreateTestStream(string content = "test file content")
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    private static ClaudeApiClient CreateClient(
        MockHttpMessageHandler handler, ClaudeApiSettings settings = null)
    {
        settings ??= DefaultSettings;
        var httpClient = new HttpClient(handler);
        var options = Options.Create(settings);
        var logger = NullLogger<ClaudeApiClient>.Instance;
        var metrics = new ConspectareMetrics();
        return new ClaudeApiClient(httpClient, options, logger, metrics);
    }

    private static string BuildTriageResponse(
        string documentType = "invoice",
        decimal confidence = 0.95m,
        bool isAccountingRelevant = true,
        int inputTokens = 500,
        int outputTokens = 50)
    {
        var response = new JsonObject
        {
            ["id"] = "msg_test123",
            ["type"] = "message",
            ["role"] = "assistant",
            ["model"] = "claude-sonnet-4-20250514",
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "tool_use",
                    ["id"] = "toolu_test",
                    ["name"] = "classify_document",
                    ["input"] = new JsonObject
                    {
                        ["document_type"] = documentType,
                        ["confidence"] = confidence,
                        ["is_accounting_relevant"] = isAccountingRelevant,
                        ["reasoning"] = "Document appears to be a standard invoice"
                    }
                }
            },
            ["usage"] = new JsonObject
            {
                ["input_tokens"] = inputTokens,
                ["output_tokens"] = outputTokens
            }
        };

        return response.ToJsonString();
    }

    private static string BuildExtractionResponse(
        int inputTokens = 1000,
        int outputTokens = 200)
    {
        var response = new JsonObject
        {
            ["id"] = "msg_test456",
            ["type"] = "message",
            ["role"] = "assistant",
            ["model"] = "claude-sonnet-4-20250514",
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "tool_use",
                    ["id"] = "toolu_test2",
                    ["name"] = "extract_invoice_data",
                    ["input"] = new JsonObject
                    {
                        ["invoice_number"] = "FAC-2024-001",
                        ["invoice_date"] = "2024-03-15",
                        ["due_date"] = "2024-04-15",
                        ["currency"] = "RON",
                        ["supplier"] = new JsonObject
                        {
                            ["name"] = "SC Furnizor SRL",
                            ["tax_id"] = "RO12345678"
                        },
                        ["customer"] = new JsonObject
                        {
                            ["name"] = "SC Client SRL",
                            ["tax_id"] = "RO87654321"
                        },
                        ["line_items"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["description"] = "Servicii consultanta",
                                ["quantity"] = 10,
                                ["unit"] = "ore",
                                ["unit_price"] = 100,
                                ["vat_rate"] = 19,
                                ["vat_amount"] = 190,
                                ["line_total"] = 1190
                            }
                        },
                        ["subtotal"] = 1000,
                        ["total_vat"] = 190,
                        ["total"] = 1190,
                        ["review_flags"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["flag_type"] = "missing_field",
                                ["severity"] = "warning",
                                ["message"] = "Payment method not specified"
                            }
                        }
                    }
                }
            },
            ["usage"] = new JsonObject
            {
                ["input_tokens"] = inputTokens,
                ["output_tokens"] = outputTokens
            }
        };

        return response.ToJsonString();
    }

    [Fact]
    public async Task TriageAsync_SuccessfulResponse_ReturnsTriageResult()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, BuildTriageResponse());
        var client = CreateClient(handler);
        var doc = CreateTestDocument();

        using var stream = CreateTestStream();
        var result = await client.TriageAsync(doc, stream, "triage_v1.0.0");

        Assert.Equal("invoice", result.DocumentType);
        Assert.Equal(0.95m, result.Confidence);
        Assert.True(result.IsAccountingRelevant);
        Assert.Equal("claude-sonnet-4-20250514", result.ModelId);
        Assert.Equal("triage_v1.0.0", result.PromptVersion);
        Assert.Equal(500, result.InputTokens);
        Assert.Equal(50, result.OutputTokens);
        Assert.NotNull(result.LatencyMs);
        Assert.True(result.LatencyMs >= 0);
    }

    [Fact]
    public async Task TriageAsync_TextDocument_SendsTextContent()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, BuildTriageResponse());
        var client = CreateClient(handler);
        var doc = CreateTestDocument("text/xml");

        using var stream = CreateTestStream("invoice test content");
        await client.TriageAsync(doc, stream, "triage_v1.0.0");

        Assert.Single(handler.Requests);
        var requestBody = handler.Requests[0];
        Assert.Contains("text", requestBody);
        Assert.Contains("invoice test content", requestBody);
        Assert.DoesNotContain("base64", requestBody);
    }

    [Fact]
    public async Task TriageAsync_ImageDocument_SendsBase64Content()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, BuildTriageResponse());
        var client = CreateClient(handler);
        var doc = CreateTestDocument("image/jpeg");

        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        using var stream = new MemoryStream(imageBytes);
        await client.TriageAsync(doc, stream, "triage_v1.0.0");

        Assert.Single(handler.Requests);
        var requestBody = handler.Requests[0];
        Assert.Contains("base64", requestBody);
        Assert.Contains("image/jpeg", requestBody);
    }

    [Fact]
    public async Task ExtractAsync_SuccessfulResponse_ReturnsExtractionResult()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, BuildExtractionResponse());
        var client = CreateClient(handler);
        var doc = CreateTestDocument();

        using var stream = CreateTestStream();
        var result = await client.ExtractAsync(doc, stream, "invoice", "extraction_v1.0.0");

        Assert.Contains("FAC-2024-001", result.OutputJson);
        Assert.Equal("1.0.0", result.SchemaVersion);
        Assert.Equal("claude-sonnet-4-20250514", result.ModelId);
        Assert.Equal("extraction_v1.0.0", result.PromptVersion);
        Assert.Equal(1000, result.InputTokens);
        Assert.Equal(200, result.OutputTokens);
        Assert.NotNull(result.LatencyMs);
        Assert.Single(result.ReviewFlags);
        Assert.Equal("missing_field", result.ReviewFlags[0].FlagType);
        Assert.Equal("warning", result.ReviewFlags[0].Severity);
    }

    [Fact]
    public async Task SendWithRetry_429ThenSuccess_RetriesAndReturns()
    {
        var responses = new Queue<(HttpStatusCode, string)>();
        responses.Enqueue((HttpStatusCode.TooManyRequests, "{\"error\":\"rate_limited\"}"));
        responses.Enqueue((HttpStatusCode.TooManyRequests, "{\"error\":\"rate_limited\"}"));
        responses.Enqueue((HttpStatusCode.OK, BuildTriageResponse()));

        var handler = new MockHttpMessageHandler(responses);
        var settings = DefaultSettings;
        settings.MaxRetries = 3;
        var client = CreateClient(handler, settings);

        var requestBody = new JsonObject { ["test"] = "data" };
        var result = await client.SendWithRetryAsync(requestBody, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task SendWithRetry_503ThenSuccess_RetriesAndReturns()
    {
        var responses = new Queue<(HttpStatusCode, string)>();
        responses.Enqueue((HttpStatusCode.ServiceUnavailable, "{\"error\":\"overloaded\"}"));
        responses.Enqueue((HttpStatusCode.OK, BuildTriageResponse()));

        var handler = new MockHttpMessageHandler(responses);
        var client = CreateClient(handler);

        var requestBody = new JsonObject { ["test"] = "data" };
        var result = await client.SendWithRetryAsync(requestBody, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task SendWithRetry_429ExhaustedRetries_ThrowsHttpRequestException()
    {
        var responses = new Queue<(HttpStatusCode, string)>();
        responses.Enqueue((HttpStatusCode.TooManyRequests, "{\"error\":\"rate_limited\"}"));
        responses.Enqueue((HttpStatusCode.TooManyRequests, "{\"error\":\"rate_limited\"}"));
        responses.Enqueue((HttpStatusCode.TooManyRequests, "{\"error\":\"rate_limited\"}"));
        responses.Enqueue((HttpStatusCode.TooManyRequests, "{\"error\":\"rate_limited\"}"));

        var handler = new MockHttpMessageHandler(responses);
        var client = CreateClient(handler);

        var requestBody = new JsonObject { ["test"] = "data" };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SendWithRetryAsync(requestBody, CancellationToken.None));

        Assert.Contains("429", ex.Message);
    }

    [Fact]
    public async Task SendWithRetry_400Error_DoesNotRetry()
    {
        var handler = new MockHttpMessageHandler(
            HttpStatusCode.BadRequest, "{\"error\":\"invalid_request\"}");
        var client = CreateClient(handler);

        var requestBody = new JsonObject { ["test"] = "data" };

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SendWithRetryAsync(requestBody, CancellationToken.None));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task TriageAsync_RequestContainsToolChoice()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, BuildTriageResponse());
        var client = CreateClient(handler);
        var doc = CreateTestDocument();

        using var stream = CreateTestStream();
        await client.TriageAsync(doc, stream, "triage_v1.0.0");

        var requestBody = handler.Requests[0];
        Assert.Contains("classify_document", requestBody);
        Assert.Contains("tool_choice", requestBody);
    }

    [Fact]
    public async Task TriageAsync_RequestContainsHeaders()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, BuildTriageResponse());
        var client = CreateClient(handler);
        var doc = CreateTestDocument();

        using var stream = CreateTestStream();
        await client.TriageAsync(doc, stream, "triage_v1.0.0");

        Assert.Contains("x-api-key", handler.RequestHeaders.Keys);
        Assert.Contains("anthropic-version", handler.RequestHeaders.Keys);
        Assert.Equal("test-key", handler.RequestHeaders["x-api-key"]);
        Assert.Equal("2023-06-01", handler.RequestHeaders["anthropic-version"]);
    }

    [Fact]
    public async Task ExtractAsync_RequestContainsDocumentType()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, BuildExtractionResponse());
        var client = CreateClient(handler);
        var doc = CreateTestDocument();

        using var stream = CreateTestStream();
        await client.ExtractAsync(doc, stream, "invoice", "extraction_v1.0.0");

        var requestBody = handler.Requests[0];
        Assert.Contains("invoice", requestBody);
        Assert.Contains("extract_invoice_data", requestBody);
    }
}

/// <summary>
/// Mock HttpMessageHandler that records requests and returns configured responses.
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode StatusCode, string Body)> _responses;
    public List<string> Requests { get; } = new();
    public List<string> RequestUrls { get; } = new();
    public Dictionary<string, string> RequestHeaders { get; } = new();

    public MockHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
    {
        _responses = new Queue<(HttpStatusCode, string)>();
        _responses.Enqueue((statusCode, responseBody));
    }

    public MockHttpMessageHandler(Queue<(HttpStatusCode, string)> responses)
    {
        _responses = responses;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content != null
            ? await request.Content.ReadAsStringAsync(cancellationToken)
            : "";
        Requests.Add(body);
        RequestUrls.Add(request.RequestUri?.ToString() ?? "");

        foreach (var header in request.Headers)
        {
            RequestHeaders[header.Key] = string.Join(",", header.Value);
        }

        var (statusCode, responseBody) = _responses.Count > 1
            ? _responses.Dequeue()
            : _responses.Peek();

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };
    }
}
