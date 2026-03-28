using System.Security.Cryptography;
using System.Text;

namespace Conspectare.Services.Auth;

public static class AuthTokenHelper
{
    /// <summary>
    /// Generates a cryptographically secure random token as a lowercase hex string (64 characters).
    /// </summary>
    public static string GenerateRawToken()
    {
        const int tokenLengthBytes = 32;
        var bytes = RandomNumberGenerator.GetBytes(tokenLengthBytes);
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>
    /// Computes a SHA-256 hash of the given raw token and returns it as a lowercase hex string.
    /// The hash is used for secure storage — the raw token is never persisted.
    /// </summary>
    public static string HashToken(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Returns a partially redacted version of an email address suitable for logging.
    /// Shows up to the first 3 characters of the local part, then masks the rest before the domain.
    /// Returns "***" for null, empty, or malformed inputs.
    /// </summary>
    public static string MaskEmail(string email)
    {
        if (string.IsNullOrEmpty(email))
            return "***";

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
            return "***";

        var localPart = email[..atIndex];
        var domain = email[atIndex..];

        // Show at most 3 characters of the local part, then append *** before the domain.
        var visibleChars = Math.Min(3, localPart.Length);
        return $"{localPart[..visibleChars]}***{domain}";
    }
}
