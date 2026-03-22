using System.Text.Json;
using System.Text.Json.Nodes;
using Conspectare.Domain.Entities;

namespace Conspectare.Services;

public static class WebhookPayloadBuilder
{
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
            ["confidence"] = doc.TriageConfidence.HasValue ? (JsonNode)doc.TriageConfidence.Value : null,
            ["completed_at"] = doc.CompletedAt?.ToString("O")
        };
        var effectiveOutput = canonicalOutput ?? doc.CanonicalOutput;
        if (effectiveOutput != null)
        {
            try
            {
                var outputNode = JsonNode.Parse(effectiveOutput.OutputJson);
                payload["result_summary"] = outputNode;
            }
            catch
            {
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
