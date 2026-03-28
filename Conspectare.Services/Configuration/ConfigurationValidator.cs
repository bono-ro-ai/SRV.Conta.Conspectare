using Microsoft.Extensions.Configuration;

namespace Conspectare.Services.Configuration;

/// <summary>
/// Validates that all required configuration keys are present at application startup.
/// Throws <see cref="InvalidOperationException"/> with a consolidated error list if any keys are missing,
/// preventing the application from starting in a misconfigured state.
/// </summary>
public static class ConfigurationValidator
{
    /// <summary>
    /// Checks all required configuration entries and throws if any are absent or empty.
    /// Validates LLM provider API keys conditionally based on the configured provider
    /// and whether multi-model mode is enabled.
    /// </summary>
    public static void Validate(IConfiguration config)
    {
        var errors = new List<string>();

        // Infrastructure required for all deployment profiles.
        ValidateRequired(config, "ConnectionStrings:ConspectareDb", errors);
        ValidateRequired(config, "Aws:BucketName", errors);
        ValidateRequired(config, "Aws:Region", errors);
        ValidateRequired(config, "Aws:AccessKeyId", errors);
        ValidateRequired(config, "Aws:SecretAccessKey", errors);

        var provider = config.GetValue<string>("Llm:Provider") ?? "claude";
        var multiModel = config.GetValue<bool>("Llm:MultiModel:Enabled");

        // Require only the API key(s) relevant to the active LLM configuration.
        if (provider.Equals("claude", StringComparison.OrdinalIgnoreCase) || multiModel)
            ValidateRequired(config, "Claude:ApiKey", errors);

        if (provider.Equals("gemini", StringComparison.OrdinalIgnoreCase) || multiModel)
            ValidateRequired(config, "Gemini:ApiKey", errors);

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Missing required configuration:\n{string.Join("\n", errors.Select(e => $"  - {e}"))}");
    }

    /// <summary>
    /// Adds <paramref name="key"/> to <paramref name="errors"/> if the corresponding configuration
    /// value is null, empty, or whitespace-only.
    /// </summary>
    private static void ValidateRequired(IConfiguration config, string key, List<string> errors)
    {
        var value = config[key];
        if (string.IsNullOrWhiteSpace(value))
            errors.Add(key);
    }
}
