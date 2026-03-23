using Conspectare.Services.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Conspectare.Tests;

public class ConfigurationValidatorTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string> overrides = null)
    {
        var defaults = new Dictionary<string, string>
        {
            ["ConnectionStrings:ConspectareDb"] = "Server=localhost;Database=test;",
            ["Aws:BucketName"] = "test-bucket",
            ["Aws:Region"] = "eu-central-1",
            ["Aws:AccessKeyId"] = "AKIA_TEST",
            ["Aws:SecretAccessKey"] = "secret_test",
            ["Llm:Provider"] = "claude",
            ["Claude:ApiKey"] = "sk-ant-test"
        };

        if (overrides != null)
        {
            foreach (var kv in overrides)
                defaults[kv.Key] = kv.Value;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(defaults)
            .Build();
    }

    [Fact]
    public void Validate_AllKeysPresent_DoesNotThrow()
    {
        var config = BuildConfig();
        ConfigurationValidator.Validate(config);
    }

    [Theory]
    [InlineData("ConnectionStrings:ConspectareDb")]
    [InlineData("Aws:BucketName")]
    [InlineData("Aws:Region")]
    [InlineData("Aws:AccessKeyId")]
    [InlineData("Aws:SecretAccessKey")]
    public void Validate_MissingRequiredKey_Throws(string missingKey)
    {
        var config = BuildConfig(new Dictionary<string, string> { [missingKey] = "" });

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigurationValidator.Validate(config));
        Assert.Contains(missingKey, ex.Message);
    }

    [Fact]
    public void Validate_ClaudeProvider_RequiresClaudeApiKey()
    {
        var config = BuildConfig(new Dictionary<string, string>
        {
            ["Llm:Provider"] = "claude",
            ["Claude:ApiKey"] = ""
        });

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigurationValidator.Validate(config));
        Assert.Contains("Claude:ApiKey", ex.Message);
    }

    [Fact]
    public void Validate_GeminiProvider_RequiresGeminiApiKey()
    {
        var config = BuildConfig(new Dictionary<string, string>
        {
            ["Llm:Provider"] = "gemini",
            ["Claude:ApiKey"] = "",
            ["Gemini:ApiKey"] = ""
        });

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigurationValidator.Validate(config));
        Assert.Contains("Gemini:ApiKey", ex.Message);
        Assert.DoesNotContain("Claude:ApiKey", ex.Message);
    }

    [Fact]
    public void Validate_GeminiProvider_DoesNotRequireClaudeApiKey()
    {
        var config = BuildConfig(new Dictionary<string, string>
        {
            ["Llm:Provider"] = "gemini",
            ["Claude:ApiKey"] = "",
            ["Gemini:ApiKey"] = "gemini-key-test"
        });

        ConfigurationValidator.Validate(config);
    }

    [Fact]
    public void Validate_DefaultProvider_RequiresClaudeApiKey()
    {
        var overrides = new Dictionary<string, string>
        {
            ["Claude:ApiKey"] = ""
        };
        var defaults = new Dictionary<string, string>
        {
            ["ConnectionStrings:ConspectareDb"] = "Server=localhost;Database=test;",
            ["Aws:BucketName"] = "test-bucket",
            ["Aws:Region"] = "eu-central-1",
            ["Aws:AccessKeyId"] = "AKIA_TEST",
            ["Aws:SecretAccessKey"] = "secret_test",
            ["Claude:ApiKey"] = ""
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(defaults)
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigurationValidator.Validate(config));
        Assert.Contains("Claude:ApiKey", ex.Message);
    }

    [Fact]
    public void Validate_MultiModelEnabled_RequiresBothApiKeys()
    {
        var config = BuildConfig(new Dictionary<string, string>
        {
            ["Llm:Provider"] = "claude",
            ["Llm:MultiModel:Enabled"] = "true",
            ["Claude:ApiKey"] = "",
            ["Gemini:ApiKey"] = ""
        });

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigurationValidator.Validate(config));
        Assert.Contains("Claude:ApiKey", ex.Message);
        Assert.Contains("Gemini:ApiKey", ex.Message);
    }

    [Fact]
    public void Validate_MultiModelEnabled_WithBothKeys_DoesNotThrow()
    {
        var config = BuildConfig(new Dictionary<string, string>
        {
            ["Llm:MultiModel:Enabled"] = "true",
            ["Gemini:ApiKey"] = "gemini-key-test"
        });

        ConfigurationValidator.Validate(config);
    }

    [Fact]
    public void Validate_MultiModelEnabledWithGeminiProvider_RequiresBothKeys()
    {
        var config = BuildConfig(new Dictionary<string, string>
        {
            ["Llm:Provider"] = "gemini",
            ["Llm:MultiModel:Enabled"] = "true",
            ["Claude:ApiKey"] = "",
            ["Gemini:ApiKey"] = ""
        });

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigurationValidator.Validate(config));
        Assert.Contains("Claude:ApiKey", ex.Message);
        Assert.Contains("Gemini:ApiKey", ex.Message);
    }

    [Fact]
    public void Validate_ReportsAllMissingKeys_NotJustFirst()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>())
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigurationValidator.Validate(config));
        Assert.Contains("ConnectionStrings:ConspectareDb", ex.Message);
        Assert.Contains("Aws:BucketName", ex.Message);
        Assert.Contains("Aws:Region", ex.Message);
        Assert.Contains("Aws:AccessKeyId", ex.Message);
        Assert.Contains("Aws:SecretAccessKey", ex.Message);
        Assert.Contains("Claude:ApiKey", ex.Message);
    }

    [Fact]
    public void Validate_WhitespaceOnlyValue_TreatedAsMissing()
    {
        var config = BuildConfig(new Dictionary<string, string>
        {
            ["Aws:BucketName"] = "   "
        });

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigurationValidator.Validate(config));
        Assert.Contains("Aws:BucketName", ex.Message);
    }
}
