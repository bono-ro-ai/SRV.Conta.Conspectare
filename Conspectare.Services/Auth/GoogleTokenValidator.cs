using Google.Apis.Auth;
using Conspectare.Services.Interfaces;

namespace Conspectare.Services.Auth;

public class GoogleTokenValidator : IGoogleTokenValidator
{
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
