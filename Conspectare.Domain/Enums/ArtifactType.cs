namespace Conspectare.Domain.Enums;

public static class ArtifactType
{
    public const string Raw = "raw";
    public const string OcrText = "ocr_text";
    public const string LlmTriageResponse = "llm_triage_response";
    public const string LlmExtractionResponse = "llm_extraction_response";
    public const string CanonicalJson = "canonical_json";
}
