using Google.Apis.Auth;
using Conspectare.Services.Interfaces;

namespace Conspectare.Services.Auth;

public class GoogleTokenValidator : IGoogleTokenValidator
{
    /// <summary>
    /// Validates a Google ID token credential against the expected client ID and returns the verified payload.
    /// Throws <see cref="InvalidJwtException"/> if the token is invalid or the audience does not match.
    /// </summary>
    public async Task<GoogleTokenPayload> ValidateAsync(string credential, string clientId)
    {
        var settings = new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { clientId } };
        var payload = await GoogleJsonWebSignature.ValidateAsync(credential, settings);

        return new GoogleTokenPayload(
            payload.Subject,
            payload.Email,
            payload.Name,
            payload.Picture,
            payload.EmailVerified,
            payload.HostedDomain);
    }
}
