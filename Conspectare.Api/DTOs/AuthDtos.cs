namespace Conspectare.Api.DTOs;

public record LoginRequest(string Email, string Password);

public record RegisterRequest(string Email, string Name, string Password);

public record AuthResponse(string Token, DateTime ExpiresAt, UserInfoResponse User);

public record UserInfoResponse(long Id, string Email, string Name, string Role);

public record MessageResponse(string Message);
