using System.Net;
using System.Text.Json.Nodes;
using Conspectare.Services.ExternalIntegrations.Anaf;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Conspectare.Tests;

public class AnafVatValidationClientTests
{
    private static AnafVatValidationSettings DefaultSettings => new()
    {
        BaseUrl = "https://webservicesp.anaf.ro/PlatitorTvaRest",
        TimeoutSeconds = 10,
        MaxRetries = 2
    };

    private static AnafVatValidationClient CreateClient(
        MockHttpMessageHandler handler, AnafVatValidationSettings settings = null)
    {
        settings ??= DefaultSettings;
        var httpClient = new HttpClient(handler);
        var options = Options.Create(settings);
        var logger = NullLogger<AnafVatValidationClient>.Instance;
        return new AnafVatValidationClient(httpClient, options, logger);
    }

    private static string BuildValidActiveResponse(string companyName = "SC EXEMPLU SRL")
    {
        var response = new JsonObject
        {
            ["cod"] = 200,
            ["message"] = "SUCCESS",
            ["found"] = new JsonArray
            {
                new JsonObject
                {
                    ["date_generale"] = new JsonObject
                    {
                        ["cui"] = 12345678,
                        ["denumire"] = companyName,
                        ["adresa"] = "Str. Test Nr. 1",
                        ["stare_inregistrare"] = "INREGISTRAT"
                    },
                    ["inactpiInactiv"] = new JsonObject
                    {
                        ["statusInactivi"] = false
                    }
                }
            },
            ["notfound"] = new JsonArray()
        };
        return response.ToJsonString();
    }

    private static string BuildInactiveCompanyResponse(string companyName = "SC INACTIV SRL")
    {
        var response = new JsonObject
        {
            ["cod"] = 200,
            ["message"] = "SUCCESS",
            ["found"] = new JsonArray
            {
                new JsonObject
                {
                    ["date_generale"] = new JsonObject
                    {
                        ["cui"] = 12345678,
                        ["denumire"] = companyName,
                        ["adresa"] = "Str. Test Nr. 1",
                        ["stare_inregistrare"] = "INREGISTRAT"
                    },
                    ["inactpiInactiv"] = new JsonObject
                    {
                        ["statusInactivi"] = true
                    }
                }
            },
            ["notfound"] = new JsonArray()
        };
        return response.ToJsonString();
    }

    private static string BuildNotFoundResponse()
    {
        var response = new JsonObject
        {
            ["cod"] = 200,
            ["message"] = "SUCCESS",
            ["found"] = new JsonArray(),
            ["notfound"] = new JsonArray { 99999999 }
        };
        return response.ToJsonString();
    }

    [Fact]
    public async Task ValidateCui_ValidActiveCui_ReturnsValid()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, BuildValidActiveResponse());
        var client = CreateClient(handler);

        var result = await client.ValidateCuiAsync("RO12345678", CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal("RO12345678", result.Cui);
        Assert.Equal("SC EXEMPLU SRL", result.CompanyName);
        Assert.False(result.IsInactive);
        Assert.Null(result.ValidationError);
    }

    [Fact]
    public async Task ValidateCui_InvalidCui_ReturnsInvalid()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, BuildNotFoundResponse());
        var client = CreateClient(handler);

        var result = await client.ValidateCuiAsync("99999999", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("99999999", result.Cui);
        Assert.Null(result.CompanyName);
        Assert.Contains("not found", result.ValidationError);
    }

    [Fact]
    public async Task ValidateCui_InactiveCompany_ReturnsInactive()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, BuildInactiveCompanyResponse());
        var client = CreateClient(handler);

        var result = await client.ValidateCuiAsync("12345678", CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.True(result.IsInactive);
        Assert.Equal("SC INACTIV SRL", result.CompanyName);
    }

    [Fact]
    public async Task ValidateCui_Timeout_ReturnsError()
    {
        var handler = new TimeoutHttpMessageHandler();
        var settings = new AnafVatValidationSettings
        {
            BaseUrl = "https://webservicesp.anaf.ro/PlatitorTvaRest",
            TimeoutSeconds = 1,
            MaxRetries = 0
        };
        var client = CreateClient(handler, settings);

        var result = await client.ValidateCuiAsync("12345678", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("timed out", result.ValidationError);
    }

    [Fact]
    public async Task ValidateCui_MalformedResponse_ReturnsError()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "not valid json {{{");
        var client = CreateClient(handler);

        var result = await client.ValidateCuiAsync("12345678", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("malformed", result.ValidationError);
    }

    [Fact]
    public async Task ValidateCui_NonNumericCui_ReturnsInvalid()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, BuildValidActiveResponse());
        var client = CreateClient(handler);

        var result = await client.ValidateCuiAsync("ABCDEF", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("not a valid numeric", result.ValidationError);
    }

    [Fact]
    public async Task ValidateCui_StripsPrefixRO()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, BuildValidActiveResponse());
        var client = CreateClient(handler);

        var result = await client.ValidateCuiAsync("RO12345678", CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Single(handler.Requests);
        Assert.Contains("12345678", handler.Requests[0]);
        Assert.DoesNotContain("\"RO", handler.Requests[0]);
    }

    [Fact]
    public void ParseResponse_ValidActiveResponse_ReturnsValid()
    {
        var result = AnafVatValidationClient.ParseResponse("12345678", BuildValidActiveResponse());

        Assert.True(result.IsValid);
        Assert.False(result.IsInactive);
        Assert.Equal("SC EXEMPLU SRL", result.CompanyName);
    }

    [Fact]
    public void ParseResponse_EmptyFoundArray_ReturnsNotFound()
    {
        var result = AnafVatValidationClient.ParseResponse("99999999", BuildNotFoundResponse());

        Assert.False(result.IsValid);
        Assert.Contains("not found", result.ValidationError);
    }

    [Fact]
    public void ParseResponse_MalformedJson_ReturnsError()
    {
        var result = AnafVatValidationClient.ParseResponse("12345678", "{{invalid}}");

        Assert.False(result.IsValid);
        Assert.Contains("malformed", result.ValidationError);
    }
}

public class TimeoutHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout");
    }
}
