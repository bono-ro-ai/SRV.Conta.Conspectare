using Conspectare.Domain.Entities;

namespace Conspectare.Services.Interfaces;

public record AuthResult(string Token, DateTime ExpiresAt, User User, string RawRefreshToken);

public interface IAuthService
{
    Task<OperationResult<AuthResult>> LoginAsync(string email, string password);
    Task<OperationResult<User>> RegisterAsync(string email, string name, string password);
    Task<OperationResult<AuthResult>> RefreshTokenAsync(string refreshToken);
    Task<OperationResult<bool>> RevokeAllAsync(long userId);
    Task<OperationResult<User>> GetUserByIdAsync(long userId);
}
