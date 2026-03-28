using System.Text.Json;
using System.Text.Json.Nodes;
using Conspectare.Domain.Entities;

namespace Conspectare.Services;

/// <summary>
/// Builds the JSON payload sent to a tenant's webhook endpoint when a document status changes.
/// Assembles a flat event object that includes document metadata, optional canonical output,
/// and any review flags associated with the document.
/// </summary>
public static class WebhookPayloadBuilder
{
    /// <summary>
    /// Constructs and returns a compact JSON string representing the <c>document.status_changed</c> event.
    /// </summary>
    /// <param name="doc">The document whose status has changed.</param>
    /// <param name="utcNow">The timestamp to record for the event (must be UTC).</param>
    /// <param name="canonicalOutput">
    /// Optional canonical output to include. Falls back to <c>doc.CanonicalOutput</c> when null.
    /// </param>
    /// <param name="reviewFlags">Optional list of review flags to include in the payload.</param>
    public static string Build(Document doc, DateTime utcNow,
        CanonicalOutput canonicalOutput = null, IList<ReviewFlag> reviewFlags = null)
    {
        var payload = new JsonObject
        {
            ["event"] = "document.status_changed",
            ["document_id"] = doc.Id,
            ["document_ref"] = doc.DocumentRef,
            ["fiscal_code"] = doc.FiscalCode,
            ["external_ref"] = doc.ExternalRef,
            ["status"] = doc.Status,
            ["timestamp"] = utcNow.ToString("O"),
            ["client_reference"] = doc.ClientReference,
            ["document_type"] = doc.DocumentType,
            // Confidence is nullable; use a conditional cast to JsonNode to avoid null serialisation issues.
            ["confidence"] = doc.TriageConfidence.HasValue ? (JsonNode)doc.TriageConfidence.Value : null,
            ["completed_at"] = doc.CompletedAt?.ToString("O")
        };

        // Prefer an explicitly passed canonical output over the one already on the document.
        var effectiveOutput = canonicalOutput ?? doc.CanonicalOutput;
        if (effectiveOutput != null)
        {
            try
            {
                // Embed the parsed JSON directly so recipients get a structured object, not an escaped string.
                var outputNode = JsonNode.Parse(effectiveOutput.OutputJson);
                payload["result_summary"] = outputNode;
            }
            catch
            {
                // If the stored JSON is malformed, omit result_summary rather than failing delivery.
                payload["result_summary"] = null;
            }

            payload["canonical_output_json"] = effectiveOutput.OutputJson;
        }

        if (doc.ErrorMessage != null)
            payload["error_message"] = doc.ErrorMessage;

        if (reviewFlags != null)
        {
            var flagsArray = new JsonArray();

            foreach (var flag in reviewFlags)
            {
                flagsArray.Add(new JsonObject
                {
                    ["flag_type"] = flag.FlagType,
                    ["severity"] = flag.Severity,
                    ["message"] = flag.Message,
                    ["is_resolved"] = flag.IsResolved
                });
            }

            payload["review_flags"] = flagsArray;
        }

        return payload.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }
}
