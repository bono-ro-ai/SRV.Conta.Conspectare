namespace Conspectare.Services.Interfaces;

public interface ITenantContext
{
    long TenantId { get; set; }
    string ApiKeyPrefix { get; set; }
    int RateLimitPerMin { get; set; }
    int MaxFileSizeMb { get; set; }
    bool IsAdmin { get; set; }
    string UserIdentity { get; set; }
}
