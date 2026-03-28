namespace Conspectare.Api.Authentication;

/// <summary>
/// Defines the authentication scheme names used across the application.
/// <list type="bullet">
///   <item><see cref="JwtBearer"/> — standard JWT Bearer token issued to human users.</item>
///   <item><see cref="ApiKey"/> — opaque API key issued to machine clients.</item>
///   <item><see cref="DualAuth"/> — policy scheme that selects between JWT and API key
///   based on the shape of the Authorization header value.</item>
/// </list>
/// </summary>
public static class AuthSchemeConstants
{
    public const string JwtBearer = "JwtBearer";
    public const string ApiKey = "ApiKey";
    public const string DualAuth = "DualAuth";
}
