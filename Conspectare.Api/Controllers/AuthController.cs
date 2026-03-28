using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Conspectare.Api.DTOs;
using Conspectare.Api.Extensions;
using Conspectare.Services.Configuration;
using Conspectare.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Conspectare.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ITenantContext _tenant;
    private readonly JwtSettings _jwtSettings;

    // In-process rate limiter for magic-link send requests, keyed by client IP.
    // Each entry tracks the request count and the start of the current 15-minute window.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _magicLinkRateLimit = new();
    private const int MagicLinkMaxPerWindow = 5;
    private static readonly TimeSpan MagicLinkWindow = TimeSpan.FromMinutes(15);

    public AuthController(IAuthService authService, ITenantContext tenant, IOptions<JwtSettings> jwtSettings)
    {
        _authService = authService;
        _tenant = tenant;
        _jwtSettings = jwtSettings.Value;
    }

    /// <summary>
    /// Authenticates a user with email and password credentials.
    /// On success, sets an HttpOnly refresh-token cookie and returns a JWT access token.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Email and password are required."
            });
        }

        var result = await _authService.LoginAsync(request.Email.Trim(), request.Password);

        if (!result.IsSuccess)
            return result.ToActionResult();

        RefreshTokenCookieHelper.SetRefreshTokenCookie(Response, result.Data.RawRefreshToken, _jwtSettings.RefreshTokenExpirationDays);

        var response = new AuthResponse(
            result.Data.Token,
            result.Data.ExpiresAt,
            new UserInfoResponse(result.Data.User.Id, result.Data.User.Email, result.Data.User.Name, result.Data.User.Role, result.Data.User.AvatarUrl));

        return Ok(response);
    }

    /// <summary>
    /// Authenticates a user via a Google ID token (OAuth2 credential).
    /// On success, sets an HttpOnly refresh-token cookie and returns a JWT access token.
    /// </summary>
    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Credential))
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Google credential is required."
            });
        }

        var result = await _authService.GoogleLoginAsync(request.Credential);

        if (!result.IsSuccess)
            return result.ToActionResult();

        RefreshTokenCookieHelper.SetRefreshTokenCookie(Response, result.Data.RawRefreshToken, _jwtSettings.RefreshTokenExpirationDays);

        var response = new AuthResponse(
            result.Data.Token,
            result.Data.ExpiresAt,
            new UserInfoResponse(result.Data.User.Id, result.Data.User.Email, result.Data.User.Name, result.Data.User.Role, result.Data.User.AvatarUrl));

        return Ok(response);
    }

    /// <summary>
    /// Registers a new tenant (company) together with its first admin user.
    /// Applies password complexity rules and returns a 201 with the generated API key on success.
    /// </summary>
    [HttpPost("signup")]
    [AllowAnonymous]
    public async Task<IActionResult> Signup([FromBody] SignupRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.CompanyName) ||
            string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Company name, email, and password are required."
            });
        }

        // Enforce minimum password complexity: length + upper + lower + digit.
        if (request.Password.Length < 10 ||
            !request.Password.Any(char.IsUpper) ||
            !request.Password.Any(char.IsLower) ||
            !request.Password.Any(char.IsDigit))
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Password must be at least 10 characters and contain at least one uppercase letter, one lowercase letter, and one digit."
            });
        }

        var result = await _authService.SignupAsync(
            request.CompanyName.Trim(),
            request.Cui?.Trim() ?? "",
            request.Email.Trim(),
            request.Password);

        if (!result.IsSuccess)
            return result.ToActionResult();

        var data = result.Data;
        RefreshTokenCookieHelper.SetRefreshTokenCookie(Response, data.RawRefreshToken, _jwtSettings.RefreshTokenExpirationDays);

        var response = new SignupResponse(
            data.TenantId,
            data.UserId,
            data.Email,
            data.Role,
            data.PlainApiKey,
            data.ApiKeyPrefix,
            data.TrialExpiresAt,
            data.Token);

        return new ObjectResult(response) { StatusCode = StatusCodes.Status201Created };
    }

    /// <summary>
    /// Registers an additional user within the caller's tenant.
    /// Only administrators may invoke this endpoint.
    /// </summary>
    [HttpPost("register")]
    [Authorize]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!_tenant.IsAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://httpstatuses.com/403",
                Title = "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = "Only administrators can register new users."
            });
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Email, name, and password are required."
            });
        }

        // Enforce minimum password complexity: length + upper + lower + digit.
        if (request.Password.Length < 10 ||
            !request.Password.Any(char.IsUpper) ||
            !request.Password.Any(char.IsLower) ||
            !request.Password.Any(char.IsDigit))
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Password must be at least 10 characters and contain at least one uppercase letter, one lowercase letter, and one digit."
            });
        }

        var result = await _authService.RegisterAsync(request.Email.Trim(), request.Name.Trim(), request.Password);

        if (!result.IsSuccess)
            return result.ToActionResult();

        var response = new MessageResponse("Registration successful.");
        return new ObjectResult(response) { StatusCode = StatusCodes.Status201Created };
    }

    /// <summary>
    /// Issues a new JWT access token using the refresh token stored in the HttpOnly cookie.
    /// Rotates the refresh token on every successful call.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh()
    {
        var rawRefreshToken = RefreshTokenCookieHelper.GetRefreshTokenFromCookie(Request);

        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Unauthorized(new ProblemDetails
            {
                Type = "https://httpstatuses.com/401",
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "No refresh token provided."
            });
        }

        var result = await _authService.RefreshTokenAsync(rawRefreshToken);

        if (!result.IsSuccess)
        {
            // Clear a stale or invalid cookie so the browser does not keep retrying.
            RefreshTokenCookieHelper.ClearRefreshTokenCookie(Response);
            return result.ToActionResult();
        }

        RefreshTokenCookieHelper.SetRefreshTokenCookie(Response, result.Data.RawRefreshToken, _jwtSettings.RefreshTokenExpirationDays);

        var response = new AuthResponse(
            result.Data.Token,
            result.Data.ExpiresAt,
            new UserInfoResponse(result.Data.User.Id, result.Data.User.Email, result.Data.User.Name, result.Data.User.Role, result.Data.User.AvatarUrl));

        return Ok(response);
    }

    /// <summary>
    /// Revokes all active refresh tokens for the authenticated user and clears the cookie.
    /// Effectively logs the user out from all sessions.
    /// </summary>
    [HttpPost("revoke")]
    [Authorize]
    public async Task<IActionResult> Revoke()
    {
        // Prefer the standard JWT sub claim; fall back to the ASP.NET identity claim.
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)
                          ?? User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        await _authService.RevokeAllAsync(userId);
        RefreshTokenCookieHelper.ClearRefreshTokenCookie(Response);

        return NoContent();
    }

    /// <summary>
    /// Sends a one-time magic-link sign-in email to the specified address.
    /// Applies an in-process per-IP rate limit of 5 requests per 15 minutes.
    /// </summary>
    [HttpPost("magic-link/send")]
    [AllowAnonymous]
    public async Task<IActionResult> SendMagicLink([FromBody] MagicLinkSendRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Email is required."
            });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var now = DateTime.UtcNow;

        // AddOrUpdate is atomic: reset the window when it has expired, otherwise increment.
        var entry = _magicLinkRateLimit.AddOrUpdate(ipAddress,
            _ => (1, now),
            (_, existing) => existing.WindowStart.Add(MagicLinkWindow) < now
                ? (1, now)
                : (existing.Count + 1, existing.WindowStart));

        if (entry.Count > MagicLinkMaxPerWindow)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Type = "https://httpstatuses.com/429",
                Title = "Too Many Requests",
                Status = StatusCodes.Status429TooManyRequests,
                Detail = "Prea multe cereri. Încercați din nou mai târziu."
            });
        }

        var result = await _authService.SendMagicLinkAsync(request.Email.Trim(), ipAddress);

        if (!result.IsSuccess)
            return result.ToActionResult();

        return Ok(new MessageResponse(result.Data));
    }

    /// <summary>
    /// Verifies a magic-link token and, on success, issues a JWT access token and refresh-token cookie.
    /// </summary>
    [HttpPost("magic-link/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyMagicLink([FromBody] MagicLinkVerifyRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Token is required."
            });
        }

        var result = await _authService.VerifyMagicLinkAsync(request.Token.Trim());

        if (!result.IsSuccess)
            return result.ToActionResult();

        RefreshTokenCookieHelper.SetRefreshTokenCookie(Response, result.Data.RawRefreshToken, _jwtSettings.RefreshTokenExpirationDays);

        var response = new AuthResponse(
            result.Data.Token,
            result.Data.ExpiresAt,
            new UserInfoResponse(result.Data.User.Id, result.Data.User.Email, result.Data.User.Name, result.Data.User.Role, result.Data.User.AvatarUrl));

        return Ok(response);
    }

    /// <summary>
    /// Returns the profile of the currently authenticated user.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        // Prefer the standard JWT sub claim; fall back to the ASP.NET identity claim.
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)
                          ?? User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        var result = await _authService.GetUserByIdAsync(userId);

        if (!result.IsSuccess)
            return result.ToActionResult();

        var user = result.Data;
        return Ok(new UserInfoResponse(user.Id, user.Email, user.Name, user.Role, user.AvatarUrl));
    }
}
