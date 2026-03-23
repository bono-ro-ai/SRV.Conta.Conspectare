namespace Conspectare.Services.Infrastructure;

public static class S3KeyBuilder
{
    public static string Input(long tenantId, string fileName)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return $"tenants/{tenantId}/input/{Guid.NewGuid()}/{fileName}";
    }

    public static string Artifact(long tenantId, long documentId, string artifactFileName)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenantId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(documentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactFileName);
        return $"tenants/{tenantId}/artifacts/{documentId}/{artifactFileName}";
    }

    public static string Output(long tenantId, long documentId, string outputFileName)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenantId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(documentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFileName);
        return $"tenants/{tenantId}/output/{documentId}/{outputFileName}";
    }
}
