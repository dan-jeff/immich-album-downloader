using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ImmichDownloader.Web.Services;

/// <summary>
/// Secure JWT service that implements proper token generation and validation
/// with comprehensive security checks and error handling.
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Generates a JWT token for an authenticated user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="username">The user's username.</param>
    /// <returns>A secure JWT token.</returns>
    string GenerateToken(int userId, string username);
    
    /// <summary>
    /// Validates a JWT token and extracts the user ID.
    /// </summary>
    /// <param name="token">The JWT token to validate.</param>
    /// <returns>The user ID if valid, null otherwise.</returns>
    int? ValidateToken(string token);
    
    /// <summary>
    /// Extracts the user ID from a valid JWT token without full validation.
    /// Use only when token has already been validated by middleware.
    /// </summary>
    /// <param name="token">The JWT token.</param>
    /// <returns>The user ID if extractable, null otherwise.</returns>
    int? ExtractUserIdFromToken(string token);
}

/// <summary>
/// Implementation of secure JWT token service with comprehensive security measures.
/// </summary>
public class JwtService : IJwtService
{
    private readonly SymmetricSecurityKey _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeSpan _tokenLifetime;
    private readonly ILogger<JwtService> _logger;

    public JwtService(IConfiguration configuration, ILogger<JwtService> logger)
    {
        _logger = logger;
        
        var secretKey = GetSecureJwtKey(configuration);
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        
        _issuer = configuration["Jwt:Issuer"] ?? "ImmichDownloader";
        _audience = configuration["Jwt:Audience"] ?? "ImmichDownloader";
        
        // Default to 24 hours, configurable
        var lifetimeHours = configuration.GetValue<int>("Jwt:TokenLifetimeHours");
        _tokenLifetime = lifetimeHours > 0 ? TimeSpan.FromHours(lifetimeHours) : TimeSpan.FromHours(24);
        
        _logger.LogInformation("JWT service initialized with {TokenLifetime} token lifetime", _tokenLifetime);
    }

    public string GenerateToken(int userId, string username)
    {
        if (userId <= 0)
        {
            throw new ArgumentException("User ID must be positive", nameof(userId));
        }
        
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be null or empty", nameof(username));
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var now = DateTime.UtcNow;
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, username),
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            }),
            Expires = now.Add(_tokenLifetime),
            NotBefore = now,
            IssuedAt = now,
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);
        
        _logger.LogInformation("JWT token generated for user {UserId} ({Username}), expires at {ExpiresAt}", 
            userId, username, tokenDescriptor.Expires);
            
        return tokenString;
    }

    public int? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _key,
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero, // No tolerance for clock skew
                RequireExpirationTime = true,
                RequireSignedTokens = true
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            
            // Additional security check: ensure token uses expected algorithm
            if (validatedToken is not JwtSecurityToken jwtToken)
            {
                _logger.LogWarning("JWT token validation failed: token is not a valid JWT security token");
                return null;
            }
            
            if (!jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                _logger.LogWarning("JWT token validation failed: unexpected algorithm {Algorithm}", jwtToken.Header.Alg);
                return null;
            }

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }
            
            _logger.LogWarning("JWT token validation failed: invalid user ID claim {UserIdClaim}", userIdClaim);
            return null;
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogInformation("JWT token expired: {Message}", ex.Message);
            return null;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning("JWT token validation failed: {Message}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during JWT token validation");
            return null;
        }
    }

    public int? ExtractUserIdFromToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            
            var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract user ID from JWT token");
            return null;
        }
    }

    /// <summary>
    /// Validates and retrieves a secure JWT key from configuration.
    /// Implements comprehensive security checks to prevent weak keys.
    /// </summary>
    private static string GetSecureJwtKey(IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
        
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "JWT secret key must be configured via Jwt:Key in appsettings.json or JWT_SECRET_KEY environment variable. " +
                "Generate a secure key with: openssl rand -base64 32");
        }
        
        // Skip validation for test environment
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? 
                         Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                         configuration["Environment"];
        
        if (string.Equals(environment, "Test", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(environment, "Testing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase) ||
            Environment.GetEnvironmentVariable("JWT_SKIP_VALIDATION") == "true")
        {
            return key;
        }
        
        // Security validations
        if (key.Length < 32)
        {
            throw new InvalidOperationException(
                $"JWT secret key must be at least 32 characters long. Current length: {key.Length}. " +
                "Generate a secure key with: openssl rand -base64 32");
        }
        
        // Detect common weak/default keys
        var weakKeys = new[]
        {
            "your-secret-key",
            "my-super-secret-jwt-key",
            "development",
            "test",
            "admin",
            "password",
            "secret",
            "key",
            "jwt-secret",
            "immich-downloader"
        };
        
        var lowerKey = key.ToLowerInvariant();
        if (weakKeys.Any(weak => lowerKey.Contains(weak)))
        {
            throw new InvalidOperationException(
                "JWT secret key appears to be a default or weak key. " +
                "Use a cryptographically secure random key. Generate one with: openssl rand -base64 32");
        }
        
        // Check for insufficient entropy (too many repeated characters)
        var uniqueChars = key.Distinct().Count();
        if (uniqueChars < key.Length * 0.3) // Less than 30% unique characters
        {
            throw new InvalidOperationException(
                "JWT secret key has insufficient entropy (too many repeated characters). " +
                "Generate a secure key with: openssl rand -base64 32");
        }
        
        return key;
    }
}