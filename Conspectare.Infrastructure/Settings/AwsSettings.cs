namespace Conspectare.Infrastructure.Settings;

public class AwsSettings
{
    public string BucketName { get; set; }
    public string Region { get; set; }
    public string AccessKeyId { get; set; }
    public string SecretAccessKey { get; set; }
    public string ServiceUrl { get; set; }
}
