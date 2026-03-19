namespace Conspectare.Infrastructure.Settings;

public class ClaudeApiSettings
{
    public string ApiKey { get; set; }
    public string Model { get; set; } = "claude-sonnet-4-20250514";
    public int MaxTokens { get; set; } = 4096;
    public string BaseUrl { get; set; } = "https://api.anthropic.com";
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxRetries { get; set; } = 3;
}
