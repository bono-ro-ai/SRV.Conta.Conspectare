namespace Conspectare.Services.Interfaces;

public interface ITenantContext
{
    long TenantId { get; set; }
    string ApiKeyPrefix { get; set; }
    int RateLimitPerMin { get; set; }
    bool IsAdmin { get; set; }
}
