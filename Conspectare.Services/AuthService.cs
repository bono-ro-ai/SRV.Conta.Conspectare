using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Conspectare.Domain.Entities;
using Conspectare.Services.Commands;
using Conspectare.Services.Configuration;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Queries;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Conspectare.Services;

public class AuthService : IAuthService
{
    private readonly JwtSettings _jwtSettings;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("dummy-timing-safe", workFactor: 12);

    public AuthService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public Task<OperationResult<AuthResult>> LoginAsync(string email, string password)
    {
        var user = new LoadUserByEmailQuery(email).Execute();

        if (user == null)
        {
            BCrypt.Net.BCrypt.Verify("dummy", DummyHash);
            return Task.FromResult(OperationResult<AuthResult>.Unauthorized("Invalid email or password."));
        }

        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
        {
            return Task.FromResult(OperationResult<AuthResult>.Unauthorized("Account is temporarily locked. Please try again later."));
        }

        if (user.LockedUntil.HasValue)
        {
            user.FailedLoginAttempts = 0;
            user.LockedUntil = null;
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
            }
            user.UpdatedAt = DateTime.UtcNow;
            new SaveUserCommand(user).Execute();
            return Task.FromResult(OperationResult<AuthResult>.Unauthorized("Invalid email or password."));
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        new SaveUserCommand(user).Execute();

        var (refreshEntity, rawRefreshToken) = CreateRefreshToken(user.Id);
        new SaveRefreshTokenCommand(refreshEntity).Execute();

        var jwtToken = GenerateJwtToken(user);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);
        return Task.FromResult(OperationResult<AuthResult>.Success(
            new AuthResult(jwtToken, expiresAt, user, rawRefreshToken)));
    }

    public Task<OperationResult<User>> RegisterAsync(string email, string name, string password)
    {
        var existing = new LoadUserByEmailQuery(email).Execute();
        if (existing != null)
        {
            return Task.FromResult(OperationResult<User>.BadRequest("A user with this email already exists."));
        }

        var hasAnyUsers = new FindAnyUserExistsQuery().Execute();
        var role = hasAnyUsers ? "user" : "admin";

        var now = DateTime.UtcNow;
        var user = new User
        {
            Email = email,
            Name = name,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
            Role = role,
            IsActive = true,
            FailedLoginAttempts = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        new SaveUserCommand(user).Execute();
        return Task.FromResult(OperationResult<User>.Created(user));
    }

    public Task<OperationResult<AuthResult>> RefreshTokenAsync(string rawRefreshToken)
    {
        var tokenHash = ComputeTokenHash(rawRefreshToken);
        var storedToken = new LoadRefreshTokenByHashQuery(tokenHash).Execute();

        if (storedToken == null)
        {
            return Task.FromResult(OperationResult<AuthResult>.Unauthorized("Invalid refresh token."));
        }

        if (storedToken.RevokedAt.HasValue)
        {
            new RevokeUserRefreshTokensCommand(storedToken.UserId).Execute();
            return Task.FromResult(OperationResult<AuthResult>.Unauthorized("Refresh token has been revoked. All sessions terminated."));
        }

        if (storedToken.ExpiresAt < DateTime.UtcNow)
        {
            return Task.FromResult(OperationResult<AuthResult>.Unauthorized("Refresh token has expired."));
        }

        var user = storedToken.User;
        if (user == null || !user.IsActive)
        {
            return Task.FromResult(OperationResult<AuthResult>.Unauthorized("User account is inactive."));
        }

        var (newRefreshEntity, newRawToken) = CreateRefreshToken(user.Id);

        using var session = Core.Database.NHibernateConspectare.OpenSession();
        using var tran = session.BeginTransaction();
        var tokenInSession = session.Get<RefreshToken>(storedToken.Id);
        tokenInSession.RevokedAt = DateTime.UtcNow;
        session.Update(tokenInSession);
        session.Save(newRefreshEntity);
        tokenInSession.ReplacedByTokenId = newRefreshEntity.Id;
        session.Update(tokenInSession);
        tran.Commit();

        var jwtToken = GenerateJwtToken(user);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);
        return Task.FromResult(OperationResult<AuthResult>.Success(
            new AuthResult(jwtToken, expiresAt, user, newRawToken)));
    }

    public Task<OperationResult<bool>> RevokeAllAsync(long userId)
    {
        new RevokeUserRefreshTokensCommand(userId).Execute();
        return Task.FromResult(OperationResult<bool>.Success(true));
    }

    public Task<OperationResult<User>> GetUserByIdAsync(long userId)
    {
        var user = new LoadUserByIdQuery(userId).Execute();
        if (user == null)
        {
            return Task.FromResult(OperationResult<User>.NotFound("User not found."));
        }
        return Task.FromResult(OperationResult<User>.Success(user));
    }

    private string GenerateJwtToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.Name),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private (RefreshToken Entity, string RawToken) CreateRefreshToken(long userId)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToHexStringLower(randomBytes);
        var hash = ComputeTokenHash(rawToken);

        var entity = new RefreshToken
        {
            UserId = userId,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow
        };

        return (entity, rawToken);
    }

    private static string ComputeTokenHash(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(hashBytes);
    }
}
