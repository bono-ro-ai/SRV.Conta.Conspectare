using Conspectare.Domain.Enums;

namespace Conspectare.Services;

public class DocumentStatusWorkflow
{
    private static readonly Dictionary<string, HashSet<string>> Transitions = new()
    {
        [DocumentStatus.Ingested] = new() { DocumentStatus.PendingTriage },
        [DocumentStatus.PendingTriage] = new() { DocumentStatus.Triaging },
        [DocumentStatus.Triaging] = new() { DocumentStatus.PendingExtraction, DocumentStatus.ReviewRequired, DocumentStatus.Rejected },
        [DocumentStatus.PendingExtraction] = new() { DocumentStatus.Extracting },
        [DocumentStatus.Extracting] = new() { DocumentStatus.Completed, DocumentStatus.ExtractionFailed, DocumentStatus.ReviewRequired },
        [DocumentStatus.ExtractionFailed] = new() { DocumentStatus.PendingTriage, DocumentStatus.Failed },
        [DocumentStatus.ReviewRequired] = new() { DocumentStatus.PendingTriage, DocumentStatus.Rejected, DocumentStatus.Completed },
    };

    private static readonly Dictionary<string, string> ExternalStatusMap = new()
    {
        [DocumentStatus.Ingested] = "processing",
        [DocumentStatus.PendingTriage] = "processing",
        [DocumentStatus.Triaging] = "processing",
        [DocumentStatus.PendingExtraction] = "processing",
        [DocumentStatus.Extracting] = "processing",
        [DocumentStatus.ExtractionFailed] = "processing",
        [DocumentStatus.Completed] = "completed",
        [DocumentStatus.Failed] = "failed",
        [DocumentStatus.ReviewRequired] = "review_required",
        [DocumentStatus.Rejected] = "rejected",
    };

    private static readonly HashSet<string> TerminalStates = new()
    {
        DocumentStatus.Completed,
        DocumentStatus.Failed,
        DocumentStatus.Rejected,
    };

    public bool CanTransition(string from, string to)
    {
        return Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    public IReadOnlyList<string> GetAvailableTransitions(string currentStatus)
    {
        if (Transitions.TryGetValue(currentStatus, out var allowed))
            return allowed.ToList().AsReadOnly();

        return Array.Empty<string>();
    }

    public string GetExternalStatus(string internalStatus)
    {
        if (ExternalStatusMap.TryGetValue(internalStatus, out var external))
            return external;

        throw new ArgumentException($"Unknown document status: '{internalStatus}'", nameof(internalStatus));
    }

    public bool IsTerminalState(string status)
    {
        return TerminalStates.Contains(status);
    }
}
