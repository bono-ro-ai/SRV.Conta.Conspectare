namespace Conspectare.Api.Extensions;

/// <summary>
/// Centralises the HttpOnly refresh-token cookie lifecycle so that all auth endpoints
/// use identical cookie attributes (name, path, security flags, and expiry).
/// Scoping the cookie path to the auth prefix prevents it from being sent on every request.
/// </summary>
public static class RefreshTokenCookieHelper
{
    private const string CookieName = "refresh_token";
    private const string CookiePath = "/api/v1/auth";

    /// <summary>
    /// Appends the refresh token as an HttpOnly, Secure, SameSite=Strict cookie that
    /// expires after <paramref name="expirationDays"/> days.
    /// </summary>
    public static void SetRefreshTokenCookie(HttpResponse response, string rawRefreshToken, int expirationDays = 7)
    {
        response.Cookies.Append(CookieName, rawRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            Expires = DateTimeOffset.UtcNow.AddDays(expirationDays)
        });
    }

    /// <summary>
    /// Removes the refresh-token cookie by overwriting it with an expired value.
    /// The cookie options must match those used when setting the cookie so the browser deletes it.
    /// </summary>
    public static void ClearRefreshTokenCookie(HttpResponse response)
    {
        response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath
        });
    }

    /// <summary>
    /// Reads the raw refresh-token value from the request cookie, or returns <c>null</c>
    /// when the cookie is absent.
    /// </summary>
    public static string GetRefreshTokenFromCookie(HttpRequest request)
    {
        return request.Cookies[CookieName];
    }
}
