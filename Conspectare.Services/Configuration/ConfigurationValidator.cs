using Microsoft.Extensions.Configuration;

namespace Conspectare.Services.Configuration;

public static class ConfigurationValidator
{
    public static void Validate(IConfiguration config)
    {
        var errors = new List<string>();

        ValidateRequired(config, "ConnectionStrings:ConspectareDb", errors);
        ValidateRequired(config, "Aws:BucketName", errors);
        ValidateRequired(config, "Aws:Region", errors);
        ValidateRequired(config, "Aws:AccessKeyId", errors);
        ValidateRequired(config, "Aws:SecretAccessKey", errors);

        var provider = config.GetValue<string>("Llm:Provider") ?? "claude";
        var multiModel = config.GetValue<bool>("Llm:MultiModel:Enabled");

        if (provider.Equals("claude", StringComparison.OrdinalIgnoreCase) || multiModel)
            ValidateRequired(config, "Claude:ApiKey", errors);
        if (provider.Equals("gemini", StringComparison.OrdinalIgnoreCase) || multiModel)
            ValidateRequired(config, "Gemini:ApiKey", errors);

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Missing required configuration:\n{string.Join("\n", errors.Select(e => $"  - {e}"))}");
    }

    private static void ValidateRequired(IConfiguration config, string key, List<string> errors)
    {
        var value = config[key];
        if (string.IsNullOrWhiteSpace(value))
            errors.Add(key);
    }
}
