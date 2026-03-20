namespace Conspectare.Services.ExternalIntegrations.Anaf;

public interface IAnafVatValidationClient
{
    Task<AnafValidationResult> ValidateCuiAsync(string cui, CancellationToken ct);
}
