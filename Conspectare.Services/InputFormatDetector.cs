using Conspectare.Domain.Enums;

namespace Conspectare.Services;

public static class InputFormatDetector
{
    public static string Detect(string fileName, string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return InputFormat.Unknown;

        var ct = contentType.Trim().ToLowerInvariant();

        if (ct is "text/xml" or "application/xml")
            return InputFormat.XmlEfactura;

        if (ct is "application/pdf")
            return InputFormat.Pdf;

        if (ct.StartsWith("image/", StringComparison.Ordinal))
            return InputFormat.Image;

        if (ct is "application/json")
            return InputFormat.Json;

        if (ct is "text/csv")
            return InputFormat.Csv;

        return InputFormat.Unknown;
    }
}
