using Conspectare.Services.Infrastructure;
using Xunit;

namespace Conspectare.Tests;

public class S3KeyBuilderTests
{
    [Fact]
    public void Input_ValidArgs_ReturnsCorrectFormat()
    {
        var key = S3KeyBuilder.Input(42, "invoice.pdf");

        Assert.StartsWith("tenants/42/input/", key);
        Assert.EndsWith("/invoice.pdf", key);
        var segments = key.Split('/');
        Assert.Equal(5, segments.Length);
        Assert.True(Guid.TryParse(segments[3], out _));
    }

    [Fact]
    public void Input_GeneratesUniqueGuid_EachCall()
    {
        var key1 = S3KeyBuilder.Input(1, "a.pdf");
        var key2 = S3KeyBuilder.Input(1, "a.pdf");

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Input_ZeroTenant_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => S3KeyBuilder.Input(0, "file.pdf"));
    }

    [Fact]
    public void Input_NegativeTenant_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => S3KeyBuilder.Input(-1, "file.pdf"));
    }

    [Fact]
    public void Input_NullFileName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => S3KeyBuilder.Input(1, null));
    }

    [Fact]
    public void Input_EmptyFileName_Throws()
    {
        Assert.Throws<ArgumentException>(() => S3KeyBuilder.Input(1, ""));
    }

    [Fact]
    public void Input_WhitespaceFileName_Throws()
    {
        Assert.Throws<ArgumentException>(() => S3KeyBuilder.Input(1, "   "));
    }

    [Fact]
    public void Artifact_ValidArgs_ReturnsCorrectFormat()
    {
        var key = S3KeyBuilder.Artifact(10, 99, "llm_extraction_response.json");

        Assert.Equal("tenants/10/artifacts/99/llm_extraction_response.json", key);
    }

    [Fact]
    public void Artifact_ZeroTenant_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => S3KeyBuilder.Artifact(0, 1, "a.json"));
    }

    [Fact]
    public void Artifact_ZeroDocumentId_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => S3KeyBuilder.Artifact(1, 0, "a.json"));
    }

    [Fact]
    public void Artifact_NullFileName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => S3KeyBuilder.Artifact(1, 1, null));
    }

    [Fact]
    public void Artifact_EmptyFileName_Throws()
    {
        Assert.Throws<ArgumentException>(() => S3KeyBuilder.Artifact(1, 1, ""));
    }

    [Fact]
    public void Output_ValidArgs_ReturnsCorrectFormat()
    {
        var key = S3KeyBuilder.Output(5, 200, "canonical.json");

        Assert.Equal("tenants/5/output/200/canonical.json", key);
    }

    [Fact]
    public void Output_ZeroTenant_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => S3KeyBuilder.Output(0, 1, "a.json"));
    }

    [Fact]
    public void Output_ZeroDocumentId_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => S3KeyBuilder.Output(1, 0, "a.json"));
    }

    [Fact]
    public void Output_NullFileName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => S3KeyBuilder.Output(1, 1, null));
    }
}
