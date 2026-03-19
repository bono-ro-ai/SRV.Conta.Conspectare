namespace Conspectare.Domain.Enums;

public static class DocumentStatus
{
    public const string Ingested = "ingested";
    public const string PendingTriage = "pending_triage";
    public const string Triaging = "triaging";
    public const string PendingExtraction = "pending_extraction";
    public const string Extracting = "extracting";
    public const string Completed = "completed";
    public const string ExtractionFailed = "extraction_failed";
    public const string ReviewRequired = "review_required";
    public const string Rejected = "rejected";
    public const string Failed = "failed";
}
