using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using ImmichDownloader.Web;
using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ImmichDownloader.Tests.Security;

/// <summary>
/// Security integration tests for JWT authentication and authorization.
/// Validates security hardening measures and token management functionality.
/// </summary>
[Trait("Category", "SecurityTest")]
public class JwtSecurityTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _connection;

    public JwtSecurityTests(WebApplicationFactory<Program> factory)
    {
        // JWT configuration is set globally in TestSetup.cs
        
        // Create and keep open SQLite in-memory connection with unique name for test isolation
        var uniqueDbName = $"TestDb_{GetType().Name}_{Guid.NewGuid():N}";
        _connection = new SqliteConnection($"DataSource={uniqueDbName};Mode=Memory;Cache=Shared");
        _connection.Open();
        
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing database registration
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                // Use the persistent SQLite in-memory connection
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                });
                
                // Ensure database is created after context is configured
                var serviceProvider = services.BuildServiceProvider();
                using var scope = serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                context.Database.EnsureCreated();
            });
        });

        _client = _factory.CreateClient();
    }


    [Fact]
    public async Task AuthenticatedEndpoint_WithValidToken_ShouldReturn200()
    {
        // Arrange
        await CreateTestUserAsync();
        var token = await GetValidTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/config");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuthenticatedEndpoint_WithoutToken_ShouldReturn401()
    {
        // Arrange - No authentication header

        // Act
        var response = await _client.GetAsync("/api/config");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthenticatedEndpoint_WithInvalidToken_ShouldReturn401()
    {
        // Arrange
        var invalidToken = "invalid.jwt.token";
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalidToken);

        // Act
        var response = await _client.GetAsync("/api/config");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthenticatedEndpoint_WithMalformedToken_ShouldReturn401()
    {
        // Arrange
        var malformedToken = "Bearer malformed-token-without-proper-structure";
        _client.DefaultRequestHeaders.Add("Authorization", malformedToken);

        // Act
        var response = await _client.GetAsync("/api/config");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthenticatedEndpoint_WithExpiredToken_ShouldReturn401()
    {
        // Arrange
        await CreateTestUserAsync();
        var expiredToken = await CreateExpiredTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        // Act
        var response = await _client.GetAsync("/api/config");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthenticatedEndpoint_WithTamperedToken_ShouldReturn401()
    {
        // Arrange
        await CreateTestUserAsync();
        var validToken = await GetValidTokenAsync();
        var tamperedToken = TamperWithToken(validToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);

        // Act
        var response = await _client.GetAsync("/api/config");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }



    [Fact]
    public void JwtService_GenerateToken_ShouldIncludeRequiredClaims()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
        
        // Act
        var token = jwtService.GenerateToken(1, "testuser");

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        
        // Check for claims by their actual values in the token - claims might use short names
        jsonToken.Claims.Should().Contain(c => c.Type == "nameid" && c.Value == "1");
        jsonToken.Claims.Should().Contain(c => c.Type == "unique_name" && c.Value == "testuser");
        jsonToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
        jsonToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Iat);
    }

    [Fact]
    public void JwtService_GenerateToken_WithInvalidUserId_ShouldThrow()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();

        // Act & Assert
        var action = () => jwtService.GenerateToken(0, "testuser");
        action.Should().Throw<ArgumentException>().WithMessage("*User ID must be positive*");
        
        var action2 = () => jwtService.GenerateToken(-1, "testuser");
        action2.Should().Throw<ArgumentException>().WithMessage("*User ID must be positive*");
    }

    [Fact]
    public void JwtService_GenerateToken_WithInvalidUsername_ShouldThrow()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();

        // Act & Assert
        var action = () => jwtService.GenerateToken(1, "");
        action.Should().Throw<ArgumentException>().WithMessage("*Username cannot be null or empty*");
        
        var action2 = () => jwtService.GenerateToken(1, null!);
        action2.Should().Throw<ArgumentException>().WithMessage("*Username cannot be null or empty*");
    }

    [Fact]
    public void JwtService_ValidateToken_WithValidToken_ShouldReturnUserId()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
        var token = jwtService.GenerateToken(1, "testuser");

        // Act
        var result = jwtService.ValidateToken(token);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public void JwtService_ValidateToken_WithInvalidToken_ShouldReturnNull()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
        var invalidToken = "invalid.jwt.token";

        // Act
        var result = jwtService.ValidateToken(invalidToken);

        // Assert
        result.Should().BeNull();
    }



    [Fact]
    public async Task Login_ShouldGenerateUniqueTokens()
    {
        // Arrange
        await CreateTestUserAsync();
        var loginRequest = new { username = "testuser", password = "TestPassword123!" };

        // Act
        var response1 = await _client.PostAsync("/api/auth/login",
            new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json"));
        var response2 = await _client.PostAsync("/api/auth/login",
            new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json"));

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var token1 = await ExtractTokenFromResponse(response1);
        var token2 = await ExtractTokenFromResponse(response2);
        
        token1.Should().NotBe(token2, "Each login should generate a unique token");
    }

    [Fact]
    public async Task Token_ShouldHaveProperExpiration()
    {
        // Arrange
        await CreateTestUserAsync();
        var token = await GetValidTokenAsync();

        // Act
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        // Assert
        jsonToken.ValidTo.Should().BeAfter(DateTime.UtcNow);
        jsonToken.ValidTo.Should().BeBefore(DateTime.UtcNow.AddDays(2)); // Should expire within reasonable time
    }

    [Fact]
    public async Task Token_ShouldUseSecureSigningAlgorithm()
    {
        // Arrange
        await CreateTestUserAsync();
        var token = await GetValidTokenAsync();

        // Act
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        // Assert
        jsonToken.Header.Alg.Should().Be(SecurityAlgorithms.HmacSha256);
    }



    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldNotRevealUserExistence()
    {
        // Arrange
        await CreateTestUserAsync();
        var invalidUserRequest = new { username = "nonexistentuser", password = "ValidPassword123!" };
        var invalidPasswordRequest = new { username = "testuser", password = "ValidPassword456!" };

        // Act
        var response1 = await _client.PostAsync("/api/auth/login",
            new StringContent(JsonSerializer.Serialize(invalidUserRequest), Encoding.UTF8, "application/json"));
        var response2 = await _client.PostAsync("/api/auth/login",
            new StringContent(JsonSerializer.Serialize(invalidPasswordRequest), Encoding.UTF8, "application/json"));

        // Assert
        // Current implementation returns Unauthorized for invalid credentials with valid passwords
        response1.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        
        // Both should return same generic error message
        content1.Should().Be(content2, "Error messages should not reveal user existence");
        // Check for actual error message returned by the application
        content1.Should().Contain("Incorrect username or password");
    }

    [Fact]
    public async Task Register_WithWeakPassword_ShouldReject()
    {
        // Arrange
        var weakPasswordRequest = new { username = "newuser", password = "123" };

        // Act
        var response = await _client.PostAsync("/api/auth/register",
            new StringContent(JsonSerializer.Serialize(weakPasswordRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Password"); // The actual error message uses "Password" (capital P)
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ShouldReject()
    {
        // Arrange
        await CreateTestUserAsync();
        var duplicateRequest = new { username = "testuser", password = "AnotherPassword123!" };

        // Act
        var response = await _client.PostAsync("/api/auth/register",
            new StringContent(JsonSerializer.Serialize(duplicateRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("already exists");
    }



    private async Task CreateTestUserAsync()
    {
        var registerRequest = new { username = "testuser", password = "TestPassword123!" };
        var response = await _client.PostAsync("/api/auth/register",
            new StringContent(JsonSerializer.Serialize(registerRequest), Encoding.UTF8, "application/json"));
        
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> GetValidTokenAsync()
    {
        var loginRequest = new { username = "testuser", password = "TestPassword123!" };
        var response = await _client.PostAsync("/api/auth/login",
            new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json"));
        
        response.EnsureSuccessStatusCode();
        return await ExtractTokenFromResponse(response);
    }

    private async Task<string> ExtractTokenFromResponse(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var loginResult = JsonSerializer.Deserialize<JsonElement>(content);
        return loginResult.GetProperty("access_token").GetString()!;
    }

    private async Task<string> CreateExpiredTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
        
        // Create a token that expires immediately
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? "test-jwt-key-for-component-testing-shared-across-all-tests");
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "testuser"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            }),
            NotBefore = DateTime.UtcNow.AddMinutes(-10), // Valid from 10 minutes ago
            Expires = DateTime.UtcNow.AddMinutes(-5), // Expired 5 minutes ago
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private string TamperWithToken(string token)
    {
        // Simple tampering: change one character in the signature
        var parts = token.Split('.');
        if (parts.Length != 3) return token;
        
        var signature = parts[2];
        var tamperedSignature = signature.Length > 0 ? 
            (signature[0] == 'A' ? 'B' : 'A') + signature.Substring(1) : 
            signature + "X";
        
        return $"{parts[0]}.{parts[1]}.{tamperedSignature}";
    }


    public void Dispose()
    {
        _connection?.Dispose();
        _client?.Dispose();
    }
}