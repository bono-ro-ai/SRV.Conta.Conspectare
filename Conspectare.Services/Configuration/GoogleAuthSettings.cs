namespace Conspectare.Services.Configuration;

public class GoogleAuthSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AllowedDomain { get; set; } = "bono.ro";
    public string AllowedGroup { get; set; } = string.Empty;
    public string ServiceAccountJson { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
}
