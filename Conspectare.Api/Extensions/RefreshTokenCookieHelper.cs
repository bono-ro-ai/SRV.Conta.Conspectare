namespace Conspectare.Api.Extensions;

public static class RefreshTokenCookieHelper
{
    private const string CookieName = "refresh_token";
    private const string CookiePath = "/api/v1/auth";

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

    public static string GetRefreshTokenFromCookie(HttpRequest request)
    {
        return request.Cookies[CookieName];
    }
}
