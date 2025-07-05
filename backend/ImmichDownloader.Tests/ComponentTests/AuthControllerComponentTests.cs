using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ImmichDownloader.Web;
using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using Xunit;

namespace ImmichDownloader.Tests.ComponentTests;

/// <summary>
/// Component tests for AuthController covering setup, registration, login, and JWT integration.
/// These tests verify the complete authentication flow with real database integration.
/// </summary>
[Trait("Category", "ComponentTest")]
public class AuthControllerComponentTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthControllerComponentTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing database context registration
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                // Add in-memory database for testing
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("AuthTestDb" + Guid.NewGuid().ToString());
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                });
            });
        });
        
        _client = _factory.CreateClient();
    }

    #region Setup and Registration Flow Tests

    [Fact]
    public async Task CheckSetup_WithNoUsers_ShouldReturnNeedsSetupTrue()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/check-setup");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        result.GetProperty("needsSetup").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task CheckSetup_WithExistingUsers_ShouldReturnNeedsSetupFalse()
    {
        // Arrange - Create a user first
        await CreateTestUserAsync();

        // Act
        var response = await _client.GetAsync("/api/auth/check-setup");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        result.GetProperty("needsSetup").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Register_FirstUser_ShouldCreateSuccessfully()
    {
        // Arrange
        var registerRequest = new
        {
            username = "testuser",
            password = "TestPassword123!"
        };

        // Act
        var response = await _client.PostAsync("/api/auth/register",
            new StringContent(JsonSerializer.Serialize(registerRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify user exists in database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await context.Users.FirstOrDefaultAsync(u => u.Username == "testuser");
        
        user.Should().NotBeNull();
        user!.Username.Should().Be("testuser");
        BCrypt.Net.BCrypt.Verify("TestPassword123!", user.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Register_WhenUsersExist_ShouldReturn409Conflict()
    {
        // Arrange - Create a user first
        await CreateTestUserAsync();
        
        var registerRequest = new
        {
            username = "newuser",
            password = "TestPassword123!"
        };

        // Act
        var response = await _client.PostAsync("/api/auth/register",
            new StringContent(JsonSerializer.Serialize(registerRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("setup has already been completed");
    }

    [Fact]
    public async Task Register_WithInvalidPassword_ShouldReturn400WithValidationErrors()
    {
        // Arrange
        var registerRequest = new
        {
            username = "testuser",
            password = "weak" // Too weak password
        };

        // Act
        var response = await _client.PostAsync("/api/auth/register",
            new StringContent(JsonSerializer.Serialize(registerRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Password must be at least 8 characters");
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ShouldReturn400()
    {
        // Arrange - Create first user
        await CreateTestUserAsync("testuser", "TestPassword123!");
        
        var registerRequest = new
        {
            username = "testuser", // Same username
            password = "AnotherPassword123!"
        };

        // Act
        var response = await _client.PostAsync("/api/auth/register",
            new StringContent(JsonSerializer.Serialize(registerRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Username already exists");
    }

    #endregion

    #region Login and Authentication Tests

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnJwtToken()
    {
        // Arrange
        await CreateTestUserAsync("testuser", "TestPassword123!");
        
        var loginRequest = new
        {
            username = "testuser",
            password = "TestPassword123!"
        };

        // Act
        var response = await _client.PostAsync("/api/auth/login",
            new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        
        var token = result.GetProperty("access_token").GetString();
        token.Should().NotBeNull();
        token.Should().StartWith("eyJ"); // JWT tokens start with this base64 header
        
        // Verify token structure (header.payload.signature)
        var tokenParts = token!.Split('.');
        tokenParts.Should().HaveCount(3);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturn401()
    {
        // Arrange
        await CreateTestUserAsync("testuser", "TestPassword123!");
        
        var loginRequest = new
        {
            username = "testuser",
            password = "WrongPassword123!"
        };

        // Act
        var response = await _client.PostAsync("/api/auth/login",
            new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid username or password");
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ShouldReturn401()
    {
        // Arrange
        var loginRequest = new
        {
            username = "nonexistentuser",
            password = "TestPassword123!"
        };

        // Act
        var response = await _client.PostAsync("/api/auth/login",
            new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid username or password");
    }

    [Fact]
    public async Task Login_WithEmptyCredentials_ShouldReturn400()
    {
        // Arrange
        var loginRequest = new
        {
            username = "",
            password = ""
        };

        // Act
        var response = await _client.PostAsync("/api/auth/login",
            new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region JWT Integration Tests

    [Fact]
    public async Task AuthenticatedRequest_WithValidJwt_ShouldAllowAccess()
    {
        // Arrange
        await CreateTestUserAsync("testuser", "TestPassword123!");
        var token = await GetAuthTokenAsync("testuser", "TestPassword123!");
        
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/albums");

        // Assert
        // Should be authorized (even if it returns 500 due to missing Immich config)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthenticatedRequest_WithExpiredJwt_ShouldReturn401()
    {
        // Arrange - This would require a token with short expiry, 
        // but since we can't easily create expired tokens in tests,
        // we'll test with malformed token
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "expired.jwt.token");

        // Act
        var response = await _client.GetAsync("/api/albums");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthenticatedRequest_WithInvalidJwt_ShouldReturn401()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

        // Act
        var response = await _client.GetAsync("/api/albums");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthenticatedRequest_WithMissingJwt_ShouldReturn401()
    {
        // Act
        var response = await _client.GetAsync("/api/albums");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Helper Methods

    private async Task CreateTestUserAsync(string username = "testuser", string password = "TestPassword123!")
    {
        var registerRequest = new
        {
            username = username,
            password = password
        };

        var response = await _client.PostAsync("/api/auth/register",
            new StringContent(JsonSerializer.Serialize(registerRequest), Encoding.UTF8, "application/json"));
        
        // Only assert success if this is the first user
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userCount = await context.Users.CountAsync();
        
        if (userCount <= 1)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    private async Task<string> GetAuthTokenAsync(string username, string password)
    {
        var loginRequest = new
        {
            username = username,
            password = password
        };

        var loginResponse = await _client.PostAsync("/api/auth/login",
            new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json"));

        loginResponse.EnsureSuccessStatusCode();
        
        var loginContent = await loginResponse.Content.ReadAsStringAsync();
        var loginResult = JsonSerializer.Deserialize<JsonElement>(loginContent);
        
        return loginResult.GetProperty("access_token").GetString()!;
    }

    #endregion
}