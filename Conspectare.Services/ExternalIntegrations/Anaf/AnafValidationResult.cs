namespace Conspectare.Services.ExternalIntegrations.Anaf;

public record AnafValidationResult(
    bool IsValid,
    string Cui,
    string CompanyName,
    bool IsInactive,
    string ValidationError);
