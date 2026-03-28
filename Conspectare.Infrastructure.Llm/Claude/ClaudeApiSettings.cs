namespace Conspectare.Infrastructure.Llm.Claude;

/// <summary>
/// Configuration settings for the Anthropic Claude API client.
/// Bound from the "Claude" configuration section.
/// </summary>
public class ClaudeApiSettings
{
    /// <summary>Gets or sets the Anthropic API key used for authentication.</summary>
    public string ApiKey { get; set; }

    /// <summary>Gets or sets the Claude model identifier to use for requests.</summary>
    public string Model { get; set; } = "claude-sonnet-4-20250514";

    /// <summary>Gets or sets the maximum number of tokens the model may generate per response.</summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>Gets or sets the base URL for the Anthropic API.</summary>
    public string BaseUrl { get; set; } = "https://api.anthropic.com";

    /// <summary>Gets or sets the per-request HTTP timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Gets or sets the maximum number of retry attempts on retryable failures (429, 503).</summary>
    public int MaxRetries { get; set; } = 3;
}
