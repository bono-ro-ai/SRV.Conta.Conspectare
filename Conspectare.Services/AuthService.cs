using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Conspectare.Domain.Entities;
using Conspectare.Services.Auth;
using Conspectare.Services.Commands;
using Conspectare.Services.Configuration;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Queries;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Conspectare.Services;

public class AuthService : IAuthService
{
    private readonly JwtSettings _jwtSettings;
    private readonly IEmailService _emailService;
    private readonly AppSettings _appSettings;
    private readonly ILogger<AuthService> _logger;
    private readonly GoogleAuthSettings _googleSettings;
    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IGoogleGroupChecker _groupChecker;
    private const int MaxFailedAttempts = 5;
    private const int MagicLinkExpiryMinutes = 15;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("dummy-timing-safe", workFactor: 12);

    public AuthService(IOptions<JwtSettings> jwtSettings, IEmailService emailService, IOptions<AppSettings> appSettings, ILogger<AuthService> logger, IOptions<GoogleAuthSettings> googleOptions, IGoogleTokenValidator googleTokenValidator, IGoogleGroupChecker groupChecker)
    {
        _jwtSettings = jwtSettings.Value;
        _emailService = emailService;
        _appSettings = appSettings.Value;
        _logger = logger;
        _googleSettings = googleOptions.Value;
        _googleTokenValidator = googleTokenValidator;
        _groupChecker = groupChecker;
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

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return Task.FromResult(OperationResult<AuthResult>.BadRequest("This account uses Google sign-in. Please log in with Google."));
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

    public Task<OperationResult<SignupResult>> SignupAsync(string companyName, string cui, string email, string password)
    {
        var existing = new LoadUserByEmailQuery(email).Execute();
        if (existing != null)
        {
            return Task.FromResult(OperationResult<SignupResult>.Conflict("A user with this email already exists."));
        }

        var normalizedCui = cui?.Trim() ?? "";
        if (normalizedCui.StartsWith("RO", StringComparison.OrdinalIgnoreCase))
            normalizedCui = normalizedCui[2..];

        var now = DateTime.UtcNow;
        var trialExpiresAt = now.AddDays(30);

        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var hexChars = Convert.ToHexStringLower(randomBytes);
        var plainKey = $"csp_{hexChars}";
        var prefix = plainKey[..8];
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plainKey));
        var hashHex = Convert.ToHexStringLower(hash);

        var apiClient = new ApiClient
        {
            Name = companyName,
            CompanyName = companyName,
            Cui = normalizedCui,
            ContactEmail = email,
            ApiKeyHash = hashHex,
            ApiKeyPrefix = prefix,
            IsActive = true,
            IsAdmin = false,
            RateLimitPerMin = 60,
            MaxFileSizeMb = 10,
            TrialExpiresAt = trialExpiresAt,
            CreatedAt = now,
            UpdatedAt = now
        };

        var user = new User
        {
            Email = email,
            Name = companyName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
            Role = "user",
            IsActive = true,
            FailedLoginAttempts = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        var (refreshEntity, rawRefreshToken) = CreateRefreshToken(user.Id);
        new SignupCommand(apiClient, user, refreshEntity).Execute();

        var jwtToken = GenerateJwtToken(user);

        return Task.FromResult(OperationResult<SignupResult>.Created(
            new SignupResult(
                apiClient.Id,
                user.Id,
                user.Email,
                user.Role,
                plainKey,
                prefix,
                trialExpiresAt,
                jwtToken,
                rawRefreshToken)));
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
        new RotateRefreshTokenCommand(storedToken.Id, newRefreshEntity).Execute();

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

    public async Task<OperationResult<string>> SendMagicLinkAsync(string email, string ipAddress)
    {
        var emailLower = email.ToLowerInvariant().Trim();
        var user = new LoadUserByEmailQuery(emailLower).Execute();
        var isNewUser = false;

        if (user == null)
        {
            var now = DateTime.UtcNow;
            user = new User
            {
                Email = emailLower,
                Name = emailLower.Split('@')[0],
                PasswordHash = null,
                Role = "user",
                IsActive = true,
                FailedLoginAttempts = 0,
                CreatedAt = now,
                UpdatedAt = now
            };
            isNewUser = true;
        }

        var rawToken = AuthTokenHelper.GenerateRawToken();
        var tokenHash = AuthTokenHelper.HashToken(rawToken);
        var magicLinkToken = new MagicLinkToken
        {
            TokenHash = tokenHash,
            Email = emailLower,
            ExpiresAt = DateTime.UtcNow.AddMinutes(MagicLinkExpiryMinutes),
            CreatedAt = DateTime.UtcNow,
            IpAddress = ipAddress
        };
        new CreateMagicLinkCommand(user, magicLinkToken, isNewUser).Execute();

        if (isNewUser)
        {
            _logger.LogInformation("Auto-created user via magic link for {MaskedEmail} (role: user, id: {UserId})",
                AuthTokenHelper.MaskEmail(emailLower), user.Id);
        }

        var frontendUrl = _appSettings.FrontendUrl.TrimEnd('/');
        var magicLinkUrl = $"{frontendUrl}/auth/magic-link?token={Uri.EscapeDataString(rawToken)}";
        await _emailService.SendMagicLinkEmailAsync(emailLower, magicLinkUrl);

        _logger.LogInformation("Magic link sent to {MaskedEmail} (id: {UserId})",
            AuthTokenHelper.MaskEmail(emailLower), user.Id);

        return OperationResult<string>.Success(
            "Dacă există un cont cu acest email, un link de autentificare a fost trimis.");
    }

    public Task<OperationResult<AuthResult>> VerifyMagicLinkAsync(string token)
    {
        var tokenHash = AuthTokenHelper.HashToken(token);
        var magicToken = new LoadMagicLinkByHashQuery(tokenHash).Execute();

        if (magicToken == null)
            return Task.FromResult(OperationResult<AuthResult>.BadRequest("Link invalid sau expirat."));

        if (magicToken.UsedAt != null)
            return Task.FromResult(OperationResult<AuthResult>.BadRequest("Link invalid sau expirat."));

        if (magicToken.ExpiresAt <= DateTime.UtcNow)
            return Task.FromResult(OperationResult<AuthResult>.BadRequest("Link invalid sau expirat."));

        var user = magicToken.User;
        if (user == null)
            return Task.FromResult(OperationResult<AuthResult>.BadRequest("Link invalid sau expirat."));

        var (refreshEntity, rawRefreshToken) = CreateRefreshToken(user.Id);
        new RedeemMagicLinkCommand(magicToken, user, refreshEntity).Execute();

        var jwtToken = GenerateJwtToken(user);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);

        _logger.LogInformation("Magic link verified for {MaskedEmail} (id: {UserId})",
            AuthTokenHelper.MaskEmail(user.Email), user.Id);

        return Task.FromResult(OperationResult<AuthResult>.Success(
            new AuthResult(jwtToken, expiresAt, user, rawRefreshToken)));
    }

    private string GenerateJwtToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.Name),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        if (user.TenantId.HasValue)
        {
            claims.Add(new Claim("tenantId", user.TenantId.Value.ToString()));
        }

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

    public async Task<OperationResult<AuthResult>> GoogleLoginAsync(string credential)
    {
        GoogleTokenPayload payload;
        try
        {
            payload = await _googleTokenValidator.ValidateAsync(credential, _googleSettings.ClientId);
        }
        catch
        {
            return OperationResult<AuthResult>.Unauthorized("Invalid Google credential.");
        }

        if (!payload.EmailVerified)
        {
            return OperationResult<AuthResult>.Unauthorized("Google email is not verified.");
        }

        if (!payload.Email.EndsWith($"@{_googleSettings.AllowedDomain}", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<AuthResult>.Forbidden($"Only @{_googleSettings.AllowedDomain} accounts are allowed.");
        }

        if (!await _groupChecker.IsMemberAsync(payload.Email))
        {
            return OperationResult<AuthResult>.Forbidden($"You must be a member of {_googleSettings.AllowedGroup} to access this application.");
        }

        var user = new LoadUserByGoogleIdQuery(payload.Subject).Execute();

        if (user == null)
        {
            user = new LoadUserByEmailQuery(payload.Email).Execute();

            if (user != null)
            {
                user.GoogleId = payload.Subject;
                user.AvatarUrl = payload.Picture;
                user.UpdatedAt = DateTime.UtcNow;
                new SaveUserCommand(user).Execute();
            }
            else
            {
                var now = DateTime.UtcNow;
                user = new User
                {
                    Email = payload.Email,
                    Name = payload.Name,
                    PasswordHash = null,
                    GoogleId = payload.Subject,
                    AvatarUrl = payload.Picture,
                    Role = "admin",
                    IsActive = true,
                    FailedLoginAttempts = 0,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                new SaveUserCommand(user).Execute();
            }
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        new SaveUserCommand(user).Execute();

        var (refreshEntity, rawRefreshToken) = CreateRefreshToken(user.Id);
        new SaveRefreshTokenCommand(refreshEntity).Execute();

        var jwtToken = GenerateJwtToken(user);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);
        return OperationResult<AuthResult>.Success(
            new AuthResult(jwtToken, expiresAt, user, rawRefreshToken));
    }

    private static string ComputeTokenHash(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(hashBytes);
    }
}
