using System.Reflection;

namespace Conspectare.Services.Processors;

/// <summary>
/// Provides versioned prompt strings for the triage and extraction pipeline stages.
/// Prompts are stored as embedded text resources and loaded lazily on first access.
/// </summary>
public static class PromptProvider
{
    private const string ResourcePrefix = "Conspectare.Services.Prompts.";
    private const string TriageVersion = "triage_v1.0.0";
    private const string ExtractionInvoiceVersion = "extraction_invoice_v1.0.0";
    private const string ExtractionReceiptVersion = "extraction_receipt_v1.0.0";

    // Lazy<T> ensures each embedded resource is read from the assembly exactly once.
    private static readonly Lazy<string> TriagePromptCache = new(() => LoadResource($"{TriageVersion}.txt"));
    private static readonly Lazy<string> ExtractionInvoiceCache = new(() => LoadResource($"{ExtractionInvoiceVersion}.txt"));
    private static readonly Lazy<string> ExtractionReceiptCache = new(() => LoadResource($"{ExtractionReceiptVersion}.txt"));

    /// <summary>
    /// Returns the current triage prompt text.
    /// </summary>
    public static string GetTriagePrompt() => TriagePromptCache.Value;

    /// <summary>
    /// Returns the extraction prompt text for the specified document type.
    /// Defaults to the invoice prompt for any unrecognised type.
    /// </summary>
    public static string GetExtractionPrompt(string documentType)
    {
        return documentType?.ToLowerInvariant() switch
        {
            "receipt" => ExtractionReceiptCache.Value,
            _ => ExtractionInvoiceCache.Value
        };
    }

    /// <summary>
    /// Returns the version identifier of the current triage prompt (used for audit/tracking).
    /// </summary>
    public static string GetTriagePromptVersion() => TriageVersion;

    /// <summary>
    /// Returns the version identifier of the extraction prompt for the specified document type.
    /// Defaults to the invoice version for any unrecognised type.
    /// </summary>
    public static string GetExtractionPromptVersion(string documentType)
    {
        return documentType?.ToLowerInvariant() switch
        {
            "receipt" => ExtractionReceiptVersion,
            _ => ExtractionInvoiceVersion
        };
    }

    /// <summary>
    /// Reads a named embedded resource from the executing assembly and returns its content as a string.
    /// Throws <see cref="InvalidOperationException"/> if the resource is not found,
    /// listing all available resource names to aid debugging.
    /// </summary>
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
