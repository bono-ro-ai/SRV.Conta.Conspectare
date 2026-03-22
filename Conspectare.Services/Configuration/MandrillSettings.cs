namespace Conspectare.Services.Configuration;

public class MandrillSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultSender { get; set; } = "noreply@conspectare.com";
    public string DefaultSenderName { get; set; } = "Conspectare";
}
