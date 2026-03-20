using System.Text.Json;
using System.Text.Json.Nodes;
using Conspectare.Domain.Entities;

namespace Conspectare.Services;

public static class WebhookPayloadBuilder
{
    public static string Build(Document doc, DateTime utcNow)
    {
        var payload = new JsonObject
        {
            ["event"] = "document.status_changed",
            ["document_id"] = doc.Id,
            ["external_ref"] = doc.ExternalRef,
            ["status"] = doc.Status,
            ["timestamp"] = utcNow.ToString("O")
        };

        if (doc.CanonicalOutput != null)
        {
            try
            {
                var outputNode = JsonNode.Parse(doc.CanonicalOutput.OutputJson);
                payload["result_summary"] = outputNode;
            }
            catch
            {
                payload["result_summary"] = null;
            }
        }

        if (doc.ErrorMessage != null)
            payload["error_message"] = doc.ErrorMessage;

        return payload.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }
}
