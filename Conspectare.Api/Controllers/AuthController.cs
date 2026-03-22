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

    public AuthController(IAuthService authService, ITenantContext tenant, IOptions<JwtSettings> jwtSettings)
    {
        _authService = authService;
        _tenant = tenant;
        _jwtSettings = jwtSettings.Value;
    }

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
            new UserInfoResponse(result.Data.User.Id, result.Data.User.Email, result.Data.User.Name, result.Data.User.Role));

        return Ok(response);
    }

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
            data.Token,
            data.RawRefreshToken);

        return new ObjectResult(response) { StatusCode = StatusCodes.Status201Created };
    }

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
            RefreshTokenCookieHelper.ClearRefreshTokenCookie(Response);
            return result.ToActionResult();
        }

        RefreshTokenCookieHelper.SetRefreshTokenCookie(Response, result.Data.RawRefreshToken, _jwtSettings.RefreshTokenExpirationDays);

        var response = new AuthResponse(
            result.Data.Token,
            result.Data.ExpiresAt,
            new UserInfoResponse(result.Data.User.Id, result.Data.User.Email, result.Data.User.Name, result.Data.User.Role));

        return Ok(response);
    }

    [HttpPost("revoke")]
    [Authorize]
    public async Task<IActionResult> Revoke()
    {
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

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
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
        return Ok(new UserInfoResponse(user.Id, user.Email, user.Name, user.Role));
    }
}
