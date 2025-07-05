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
/// Basic component tests for authentication that use in-memory database.
/// These tests verify the full authentication flow without external dependencies.
/// </summary>
[Trait("Category", "ComponentTest")]
public class BasicAuthComponentTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public BasicAuthComponentTests(WebApplicationFactory<Program> factory)
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
                    options.UseInMemoryDatabase("TestDb" + Guid.NewGuid().ToString());
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                });
            });
        });
        
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task AuthFlow_CompleteUserRegistrationAndLogin_ShouldWork()
    {
        // Arrange - Check setup endpoint
        var setupResponse = await _client.GetAsync("/api/auth/check-setup");
        setupResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var setupContent = await setupResponse.Content.ReadAsStringAsync();
        var setupResult = JsonSerializer.Deserialize<JsonElement>(setupContent);
        setupResult.GetProperty("needsSetup").GetBoolean().Should().BeTrue();

        // Act 1 - Create user account
        var createUserRequest = new
        {
            username = "testuser",
            password = "TestPassword123!"
        };

        var createUserResponse = await _client.PostAsync("/api/auth/create-user",
            new StringContent(JsonSerializer.Serialize(createUserRequest), Encoding.UTF8, "application/json"));

        // Assert - User creation successful
        createUserResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act 2 - Login with created user
        var loginRequest = new
        {
            username = "testuser",
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
        protectedResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
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
            username = "testuser",
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
        
        var user = await context.Users.FirstOrDefaultAsync(u => u.Username == "testuser");

        // Assert
        user.Should().NotBeNull();
        user!.Username.Should().Be("testuser");
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
            username = "testuser",
            password = "TestPassword123!"
        };

        var response = await _client.PostAsync("/api/auth/create-user",
            new StringContent(JsonSerializer.Serialize(createUserRequest), Encoding.UTF8, "application/json"));
        
        response.EnsureSuccessStatusCode();
    }
}