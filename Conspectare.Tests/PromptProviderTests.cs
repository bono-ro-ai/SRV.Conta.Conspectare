using Conspectare.Services.Processors;
using Xunit;

namespace Conspectare.Tests;

public class PromptProviderTests
{
    [Fact]
    public void GetTriagePrompt_ReturnsNonEmpty()
    {
        var prompt = PromptProvider.GetTriagePrompt();

        Assert.False(string.IsNullOrWhiteSpace(prompt));
        Assert.Contains("classify", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetExtractionPrompt_Invoice_ReturnsNonEmpty()
    {
        var prompt = PromptProvider.GetExtractionPrompt("invoice");

        Assert.False(string.IsNullOrWhiteSpace(prompt));
        Assert.Contains("invoice", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("line item", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetExtractionPrompt_Receipt_ReturnsNonEmpty()
    {
        var prompt = PromptProvider.GetExtractionPrompt("receipt");

        Assert.False(string.IsNullOrWhiteSpace(prompt));
        Assert.Contains("receipt", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bon fiscal", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetExtractionPrompt_Receipt_DiffersFromInvoice()
    {
        var invoicePrompt = PromptProvider.GetExtractionPrompt("invoice");
        var receiptPrompt = PromptProvider.GetExtractionPrompt("receipt");

        Assert.NotEqual(invoicePrompt, receiptPrompt);
    }

    [Fact]
    public void GetExtractionPrompt_UnknownType_FallsBackToInvoice()
    {
        var invoicePrompt = PromptProvider.GetExtractionPrompt("invoice");
        var unknownPrompt = PromptProvider.GetExtractionPrompt("proforma");

        Assert.Equal(invoicePrompt, unknownPrompt);
    }

    [Fact]
    public void GetTriagePromptVersion_ReturnsExpectedFormat()
    {
        var version = PromptProvider.GetTriagePromptVersion();

        Assert.Equal("triage_v1.0.0", version);
    }

    [Fact]
    public void GetExtractionPromptVersion_Invoice_ReturnsExpectedFormat()
    {
        var version = PromptProvider.GetExtractionPromptVersion("invoice");

        Assert.Equal("extraction_invoice_v2.0.0", version);
    }

    [Fact]
    public void GetExtractionPromptVersion_Receipt_ReturnsExpectedFormat()
    {
        var version = PromptProvider.GetExtractionPromptVersion("receipt");

        Assert.Equal("extraction_receipt_v2.0.0", version);
    }

    [Fact]
    public void VersionStrings_MatchFilenames()
    {
        // Verify that version strings correspond to actual embedded resource filenames
        var triageVersion = PromptProvider.GetTriagePromptVersion();
        var invoiceVersion = PromptProvider.GetExtractionPromptVersion("invoice");
        var receiptVersion = PromptProvider.GetExtractionPromptVersion("receipt");

        // If the version doesn't match a real file, GetTriagePrompt/GetExtractionPrompt would throw
        Assert.False(string.IsNullOrWhiteSpace(PromptProvider.GetTriagePrompt()));
        Assert.False(string.IsNullOrWhiteSpace(PromptProvider.GetExtractionPrompt("invoice")));
        Assert.False(string.IsNullOrWhiteSpace(PromptProvider.GetExtractionPrompt("receipt")));

        // Versions should follow the naming convention: {phase}_v{semver}
        Assert.Matches(@"^triage_v\d+\.\d+\.\d+$", triageVersion);
        Assert.Matches(@"^extraction_invoice_v\d+\.\d+\.\d+$", invoiceVersion);
        Assert.Matches(@"^extraction_receipt_v\d+\.\d+\.\d+$", receiptVersion);
    }

    [Fact]
    public void GetTriagePrompt_IsCached_ReturnsSameInstance()
    {
        var first = PromptProvider.GetTriagePrompt();
        var second = PromptProvider.GetTriagePrompt();

        Assert.True(ReferenceEquals(first, second),
            "PromptProvider should cache prompt strings via Lazy<T>");
    }
}
