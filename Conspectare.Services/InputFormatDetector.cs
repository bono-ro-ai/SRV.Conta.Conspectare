using Conspectare.Domain.Enums;

namespace Conspectare.Services;

/// <summary>
/// Maps an HTTP content-type header to one of the known <see cref="InputFormat"/> constants.
/// The file name is accepted for future extension (e.g. extension-based fallback) but is not
/// currently used in the detection logic.
/// </summary>
public static class InputFormatDetector
{
    /// <summary>
    /// Determines the pipeline input format from the MIME content type.
    /// Returns <see cref="InputFormat.Unknown"/> when the content type is absent or unrecognised.
    /// </summary>
    public static string Detect(string fileName, string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return InputFormat.Unknown;

        var ct = contentType.Trim().ToLowerInvariant();

        if (ct is "text/xml" or "application/xml")
            return InputFormat.XmlEfactura;

        if (ct is "application/pdf")
            return InputFormat.Pdf;

        // Any image/* MIME type (jpeg, png, webp, tiff, …) is routed to the image processor.
        if (ct.StartsWith("image/", StringComparison.Ordinal))
            return InputFormat.Image;

        if (ct is "application/json")
            return InputFormat.Json;

        if (ct is "text/csv")
            return InputFormat.Csv;

        return InputFormat.Unknown;
    }
}
