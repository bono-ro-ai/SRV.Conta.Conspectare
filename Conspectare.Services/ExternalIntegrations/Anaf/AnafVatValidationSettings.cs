namespace Conspectare.Services.ExternalIntegrations.Anaf;

public class AnafVatValidationSettings
{
    public string BaseUrl { get; set; } = "https://webservicesp.anaf.ro/PlatitorTvaRest";
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxRetries { get; set; } = 2;
}
