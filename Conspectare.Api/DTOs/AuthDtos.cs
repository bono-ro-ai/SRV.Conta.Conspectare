namespace Conspectare.Api.DTOs;

public record LoginRequest(string Email, string Password);

public record RegisterRequest(string Email, string Name, string Password);

public record AuthResponse(string Token, DateTime ExpiresAt, UserInfoResponse User);

public record UserInfoResponse(long Id, string Email, string Name, string Role, string AvatarUrl);

public record MessageResponse(string Message);

public record SignupRequest(string CompanyName, string Cui, string Email, string Password);

public record SignupResponse(
    long TenantId,
    long UserId,
    string Email,
    string Role,
    string ApiKey,
    string ApiKeyPrefix,
    DateTime TrialExpiresAt,
    string Token);

public record GoogleLoginRequest(string Credential);

public record MagicLinkSendRequest(string Email);

public record MagicLinkVerifyRequest(string Token);
