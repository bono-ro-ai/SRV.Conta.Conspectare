namespace Conspectare.Infrastructure.Llm.Gemini;
public class GeminiApiSettings
{
    public string ApiKey { get; set; }
    public string Model { get; set; } = "gemini-3.1-pro";
    public int MaxOutputTokens { get; set; } = 4096;
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxRetries { get; set; } = 3;
}
