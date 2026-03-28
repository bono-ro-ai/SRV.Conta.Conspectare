namespace Conspectare.Infrastructure.Llm.Gemini;

/// <summary>
/// Configuration settings for the Google Gemini API client.
/// Bound from the "Gemini" configuration section.
/// </summary>
public class GeminiApiSettings
{
    /// <summary>Gets or sets the Google API key used for authentication.</summary>
    public string ApiKey { get; set; }

    /// <summary>Gets or sets the Gemini model identifier used for extraction requests.</summary>
    public string Model { get; set; } = "gemini-2.5-flash";

    /// <summary>
    /// Gets or sets an optional model override used exclusively for triage requests.
    /// When empty or null, <see cref="Model"/> is used for triage as well.
    /// </summary>
    public string TriageModel { get; set; } = "";

    /// <summary>Gets or sets the maximum number of tokens the model may generate per response.</summary>
    public int MaxOutputTokens { get; set; } = 4096;

    /// <summary>Gets or sets the base URL for the Gemini generative language API.</summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";

    /// <summary>Gets or sets the per-request HTTP timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Gets or sets the maximum number of retry attempts on retryable failures (429, 503).</summary>
    public int MaxRetries { get; set; } = 3;
}
