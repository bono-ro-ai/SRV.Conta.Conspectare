namespace Conspectare.Services.Interfaces;

public interface ITenantContext
{
    long TenantId { get; set; }
    string ApiKeyPrefix { get; set; }
}
