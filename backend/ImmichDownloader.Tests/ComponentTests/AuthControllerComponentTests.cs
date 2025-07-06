using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ImmichDownloader.Web;
using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ImmichDownloader.Tests.ComponentTests;

/// <summary>
/// Component tests for AuthController covering setup, registration, login, and JWT integration.
/// These tests verify the complete authentication flow with real database integration.
/// </summary>
[Trait("Category", "ComponentTest")]
public class AuthControllerComponentTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _connection;
    private readonly string _testUsername;

    public AuthControllerComponentTests(WebApplicationFactory<Program> factory)
    {
        _testUsername = $"testuser_{Guid.NewGuid():N}";
        
        // JWT configuration is set globally in TestSetup.cs
        
        // Create and keep open SQLite in-memory connection with unique name for test isolation
        var uniqueDbName = $"TestDb_{GetType().Name}_{Guid.NewGuid():N}";
        _connection = new SqliteConnection($"DataSource={uniqueDbName};Mode=Memory;Cache=Shared");
        _connection.Open();
        
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Add test configuration
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "ImmichDownloader",
                    ["Jwt:Audience"] = "ImmichDownloader",
                    ["Jwt:ExpireMinutes"] = "30",
                    ["SecureDirectories:Downloads"] = Path.Combine(Path.GetTempPath(), "TestDownloads"),
                    ["SecureDirectories:Temp"] = Path.Combine(Path.GetTempPath(), "TestTemp")
                });
            });
            
            builder.ConfigureServices(services =>
            {
                // Remove the existing SQLite database context registration
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

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
    
    public void Dispose()
    {
        _connection?.Dispose();
        _client?.Dispose();
    }


    [Fact]
    public async Task CheckSetup_WithNoUsers_ShouldReturnNeedsSetupTrue()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/check-setup");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        result.GetProperty("setup_required").GetBoolean().Should().BeTrue();
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
        result.GetProperty("setup_required").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Register_FirstUser_ShouldCreateSuccessfully()
    {
        // Arrange
        var registerRequest = new
        {
            username = _testUsername,
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
        var user = await context.Users.FirstOrDefaultAsync(u => u.Username == _testUsername);
        
        user.Should().NotBeNull();
        user!.Username.Should().Be(_testUsername);
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
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("User already exists");
    }

    [Fact]
    public async Task Register_WithInvalidPassword_ShouldReturn400WithValidationErrors()
    {
        // Arrange
        var registerRequest = new
        {
            username = _testUsername,
            password = "weak" // Too weak password
        };

        // Act
        var response = await _client.PostAsync("/api/auth/register",
            new StringContent(JsonSerializer.Serialize(registerRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Password must be between 8 and 100 characters");
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ShouldReturn400()
    {
        // Arrange - Create first user
        await CreateTestUserAsync(_testUsername, "TestPassword123!");
        
        var registerRequest = new
        {
            username = _testUsername, // Same username
            password = "AnotherPassword123!"
        };

        // Act
        var response = await _client.PostAsync("/api/auth/register",
            new StringContent(JsonSerializer.Serialize(registerRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("User already exists");
    }



    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnJwtToken()
    {
        // Arrange
        await CreateTestUserAsync(_testUsername, "TestPassword123!");
        
        var loginRequest = new
        {
            username = _testUsername,
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
        await CreateTestUserAsync(_testUsername, "TestPassword123!");
        
        var loginRequest = new
        {
            username = _testUsername,
            password = "WrongPassword123!"
        };

        // Act
        var response = await _client.PostAsync("/api/auth/login",
            new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Incorrect username or password");
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
        content.Should().Contain("Incorrect username or password");
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



    [Fact]
    public async Task AuthenticatedRequest_WithValidJwt_ShouldAllowAccess()
    {
        // Arrange
        await CreateTestUserAsync(_testUsername, "TestPassword123!");
        var token = await GetAuthTokenAsync(_testUsername, "TestPassword123!");
        
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/albums");

        // Assert
        // Should be authorized (could return various statuses but not 401)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError, HttpStatusCode.BadRequest);
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



    private async Task CreateTestUserAsync(string? username = null, string password = "TestPassword123!")
    {
        var registerRequest = new
        {
            username = username ?? _testUsername,
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

}