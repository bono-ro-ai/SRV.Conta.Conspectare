using Conspectare.Domain.Entities;

namespace Conspectare.Services.Interfaces;

public record AuthResult(string Token, DateTime ExpiresAt, User User, string RawRefreshToken);

public record SignupResult(
    long TenantId,
    long UserId,
    string Email,
    string Role,
    string PlainApiKey,
    string ApiKeyPrefix,
    DateTime TrialExpiresAt,
    string Token,
    string RawRefreshToken);

public interface IAuthService
{
    Task<OperationResult<AuthResult>> LoginAsync(string email, string password);
    Task<OperationResult<User>> RegisterAsync(string email, string name, string password);
    Task<OperationResult<SignupResult>> SignupAsync(string companyName, string cui, string email, string password);
    Task<OperationResult<AuthResult>> RefreshTokenAsync(string refreshToken);
    Task<OperationResult<bool>> RevokeAllAsync(long userId);
    Task<OperationResult<User>> GetUserByIdAsync(long userId);
    Task<OperationResult<string>> SendMagicLinkAsync(string email, string ipAddress);
    Task<OperationResult<AuthResult>> VerifyMagicLinkAsync(string token);
    Task<OperationResult<AuthResult>> GoogleLoginAsync(string credential);
}
