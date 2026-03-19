using Conspectare.Services.Interfaces;

namespace Conspectare.Services;

public class TenantContext : ITenantContext
{
    public long TenantId { get; set; }
    public string ApiKeyPrefix { get; set; }
    public int RateLimitPerMin { get; set; }
}
