namespace Conspectare.Services.Interfaces;

public interface IGoogleTokenValidator
{
    Task<GoogleTokenPayload> ValidateAsync(string credential, string clientId);
}

public record GoogleTokenPayload(string Subject, string Email, string Name, string Picture, bool EmailVerified, string HostedDomain);
