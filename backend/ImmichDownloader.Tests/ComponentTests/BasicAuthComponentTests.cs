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
/// Basic component tests for authentication that use in-memory database.
/// These tests verify the full authentication flow without external dependencies.
/// </summary>
[Trait("Category", "ComponentTest")]
public class BasicAuthComponentTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _connection;
    private readonly string _testUsername;

    public BasicAuthComponentTests(WebApplicationFactory<Program> factory)
    {
        _testUsername = $"testuser_{Guid.NewGuid():N}";
        
        // Set JWT configuration for testing
        Environment.SetEnvironmentVariable("JWT_SECRET_KEY", "test-jwt-key-for-component-testing-shared-across-all-tests");
        Environment.SetEnvironmentVariable("JWT_SKIP_VALIDATION", "true");
        
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
    public async Task AuthFlow_CompleteUserRegistrationAndLogin_ShouldWork()
    {
        // Arrange - Check setup endpoint
        var setupResponse = await _client.GetAsync("/api/auth/check-setup");
        setupResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var setupContent = await setupResponse.Content.ReadAsStringAsync();
        var setupResult = JsonSerializer.Deserialize<JsonElement>(setupContent);
        setupResult.GetProperty("setup_required").GetBoolean().Should().BeTrue();

        // Act 1 - Create user account
        var createUserRequest = new
        {
            username = _testUsername,
            password = "TestPassword123!"
        };

        var createUserResponse = await _client.PostAsync("/api/auth/register",
            new StringContent(JsonSerializer.Serialize(createUserRequest), Encoding.UTF8, "application/json"));

        // Assert - User creation successful
        createUserResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act 2 - Login with created user
        var loginRequest = new
        {
            username = _testUsername,
            password = "TestPassword123!"
        };

        var loginResponse = await _client.PostAsync("/api/auth/login",
            new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json"));

        // Assert - Login successful
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var loginContent = await loginResponse.Content.ReadAsStringAsync();
        var loginResult = JsonSerializer.Deserialize<JsonElement>(loginContent);
        
        var token = loginResult.GetProperty("access_token").GetString();
        token.Should().NotBeNull();
        token.Should().StartWith("eyJ"); // JWT tokens start with this

        // Act 3 - Access protected endpoint with token
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var protectedResponse = await _client.GetAsync("/api/albums");
        
        // Assert - Can access protected endpoint (even if it returns error due to no Immich config)
        protectedResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
        // The point is we're authenticated, not that Immich is configured
    }

    [Fact]
    public async Task AuthFlow_InvalidCredentials_ShouldReturnUnauthorized()
    {
        // Arrange - Create a user first
        await CreateTestUser();

        // Act - Try to login with wrong password
        var loginRequest = new
        {
            username = _testUsername,
            password = "WrongPassword123!"
        };

        var loginResponse = await _client.PostAsync("/api/auth/login",
            new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json"));

        // Assert
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthFlow_AccessProtectedEndpointWithoutToken_ShouldReturnUnauthorized()
    {
        // Act - Try to access protected endpoint without authentication
        var response = await _client.GetAsync("/api/albums");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthFlow_AccessProtectedEndpointWithInvalidToken_ShouldReturnUnauthorized()
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
    public async Task Database_UserPersistence_ShouldStoreUserCorrectly()
    {
        // Arrange
        await CreateTestUser();

        // Act - Verify user exists in database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var user = await context.Users.FirstOrDefaultAsync(u => u.Username == _testUsername);

        // Assert
        user.Should().NotBeNull();
        user!.Username.Should().Be(_testUsername);
        user.PasswordHash.Should().NotBeNullOrEmpty();
        BCrypt.Net.BCrypt.Verify("TestPassword123!", user.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Configuration_AppSettings_ShouldPersistCorrectly()
    {
        // Arrange & Act
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        await context.AppSettings.AddAsync(new AppSetting 
        { 
            Key = "Test:Setting", 
            Value = "TestValue" 
        });
        await context.SaveChangesAsync();

        // Assert
        var setting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "Test:Setting");
        setting.Should().NotBeNull();
        setting!.Value.Should().Be("TestValue");
    }

    private async Task CreateTestUser()
    {
        var createUserRequest = new
        {
            username = _testUsername,
            password = "TestPassword123!"
        };

        var response = await _client.PostAsync("/api/auth/register",
            new StringContent(JsonSerializer.Serialize(createUserRequest), Encoding.UTF8, "application/json"));
        
        response.EnsureSuccessStatusCode();
    }
}