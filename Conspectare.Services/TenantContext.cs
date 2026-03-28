using Conspectare.Services.Interfaces;

namespace Conspectare.Services;

/// <summary>
/// Mutable per-request context populated by the authentication middleware.
/// Holds the resolved tenant identity and runtime limits for the current API call.
/// Registered as a scoped service so each request gets its own instance.
/// </summary>
public class TenantContext : ITenantContext
{
    public long TenantId { get; set; }
    public string ApiKeyPrefix { get; set; }
    public int RateLimitPerMin { get; set; }
    public int MaxFileSizeMb { get; set; }
    public bool IsAdmin { get; set; }
    public string UserIdentity { get; set; }
}
