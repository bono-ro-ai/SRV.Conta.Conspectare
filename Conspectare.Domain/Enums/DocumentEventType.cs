namespace Conspectare.Domain.Enums;

public static class DocumentEventType
{
    public const string Ingested = "ingested";
    public const string StatusChange = "status_change";
    public const string Resolved = "resolved";
    public const string CanonicalOutputEdited = "canonical_output_edited";
    public const string VatValidationCompleted = "vat_validation_completed";
}
