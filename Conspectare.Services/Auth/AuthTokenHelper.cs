using System.Security.Cryptography;
using System.Text;

namespace Conspectare.Services.Auth;

public static class AuthTokenHelper
{
    public static string GenerateRawToken()
    {
        const int tokenLengthBytes = 32;
        var bytes = RandomNumberGenerator.GetBytes(tokenLengthBytes);
        return Convert.ToHexStringLower(bytes);
    }

    public static string HashToken(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    public static string MaskEmail(string email)
    {
        if (string.IsNullOrEmpty(email))
            return "***";
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
            return "***";
        var localPart = email[..atIndex];
        var domain = email[atIndex..];
        var visibleChars = Math.Min(3, localPart.Length);
        return $"{localPart[..visibleChars]}***{domain}";
    }
}
