using System.Reflection;

namespace Conspectare.Services.Processors;

public static class PromptProvider
{
    private const string ResourcePrefix = "Conspectare.Services.Prompts.";
    private const string TriageVersion = "triage_v1.0.0";
    private const string ExtractionInvoiceVersion = "extraction_invoice_v2.0.0";
    private const string ExtractionReceiptVersion = "extraction_receipt_v2.0.0";

    private static readonly Lazy<string> TriagePromptCache = new(() => LoadResource($"{TriageVersion}.txt"));
    private static readonly Lazy<string> ExtractionInvoiceCache = new(() => LoadResource($"{ExtractionInvoiceVersion}.txt"));
    private static readonly Lazy<string> ExtractionReceiptCache = new(() => LoadResource($"{ExtractionReceiptVersion}.txt"));

    public static string GetTriagePrompt() => TriagePromptCache.Value;

    public static string GetExtractionPrompt(string documentType)
    {
        return documentType?.ToLowerInvariant() switch
        {
            "receipt" => ExtractionReceiptCache.Value,
            _ => ExtractionInvoiceCache.Value
        };
    }

    public static string GetTriagePromptVersion() => TriageVersion;

    public static string GetExtractionPromptVersion(string documentType)
    {
        return documentType?.ToLowerInvariant() switch
        {
            "receipt" => ExtractionReceiptVersion,
            _ => ExtractionInvoiceVersion
        };
    }

    private static string LoadResource(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"{ResourcePrefix}{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found. " +
                $"Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
