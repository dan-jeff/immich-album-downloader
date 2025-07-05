using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using ImmichDownloader.Tests.Infrastructure;
using ImmichDownloader.Web;
using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ImmichDownloader.Tests.ComponentTests;

/// <summary>
/// Component tests for ConfigController that verify configuration management,
/// connection testing, and resize profile CRUD operations.
/// </summary>
[Trait("Category", "ComponentTest")]
public class ConfigControllerComponentTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly MockImmichServer _mockImmichServer;

    public ConfigControllerComponentTests(WebApplicationFactory<Program> factory)
    {
        _mockImmichServer = new MockImmichServer();
        
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
                    options.UseInMemoryDatabase("ConfigTestDb" + Guid.NewGuid().ToString());
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                });
            });
        });
        
        _client = _factory.CreateClient();
    }

    #region Configuration Management Tests

    [Fact]
    public async Task GetConfig_ShouldReturnCurrentSettings()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        await SeedConfigurationAsync();

        // Act
        var response = await _client.GetAsync("/api/config");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var config = JsonSerializer.Deserialize<JsonElement>(content);
        
        config.GetProperty("immichUrl").GetString().Should().Be("https://demo.immich.app");
        config.GetProperty("immichApiKey").GetString().Should().Be("test-api-key-123");
    }

    [Fact]
    public async Task SaveConfig_WithValidSettings_ShouldPersistToDatabase()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        
        var configRequest = new
        {
            immichUrl = _mockImmichServer.BaseUrl,
            immichApiKey = "new-api-key-456"
        };

        // Act
        var response = await _client.PostAsync("/api/config",
            new StringContent(JsonSerializer.Serialize(configRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify persisted to database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var urlSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "Immich:Url");
        var apiKeySetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "Immich:ApiKey");
        
        urlSetting.Should().NotBeNull();
        urlSetting!.Value.Should().Be(_mockImmichServer.BaseUrl);
        apiKeySetting.Should().NotBeNull();
        apiKeySetting!.Value.Should().Be("new-api-key-456");
    }

    [Fact]
    public async Task SaveConfig_WithInvalidUrl_ShouldReturn400()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        
        var configRequest = new
        {
            immichUrl = "not-a-valid-url",
            immichApiKey = "test-api-key"
        };

        // Act
        var response = await _client.PostAsync("/api/config",
            new StringContent(JsonSerializer.Serialize(configRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid URL format");
    }

    [Fact]
    public async Task SaveConfig_WithEmptyApiKey_ShouldReturn400()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        
        var configRequest = new
        {
            immichUrl = _mockImmichServer.BaseUrl,
            immichApiKey = ""
        };

        // Act
        var response = await _client.PostAsync("/api/config",
            new StringContent(JsonSerializer.Serialize(configRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("API key is required");
    }

    #endregion

    #region Connection Testing Tests

    [Fact]
    public async Task TestConnection_WithValidCredentials_ShouldReturnSuccess()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        
        var testRequest = new
        {
            immichUrl = _mockImmichServer.BaseUrl,
            immichApiKey = "test-api-key"
        };

        // Act
        var response = await _client.PostAsync("/api/config/test",
            new StringContent(JsonSerializer.Serialize(testRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        result.GetProperty("success").GetBoolean().Should().BeTrue();
        result.GetProperty("message").GetString().Should().Contain("Connection successful");
    }

    [Fact]
    public async Task TestConnection_WithInvalidCredentials_ShouldReturnError()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        _mockImmichServer.SimulateAuthenticationErrors();
        
        var testRequest = new
        {
            immichUrl = _mockImmichServer.BaseUrl,
            immichApiKey = "invalid-api-key"
        };

        // Act
        var response = await _client.PostAsync("/api/config/test",
            new StringContent(JsonSerializer.Serialize(testRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        result.GetProperty("success").GetBoolean().Should().BeFalse();
        result.GetProperty("message").GetString().Should().Contain("Connection failed");
    }

    [Fact]
    public async Task TestConnection_WithServerDown_ShouldReturnError()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        _mockImmichServer.SimulateServerErrors();
        
        var testRequest = new
        {
            immichUrl = _mockImmichServer.BaseUrl,
            immichApiKey = "test-api-key"
        };

        // Act
        var response = await _client.PostAsync("/api/config/test",
            new StringContent(JsonSerializer.Serialize(testRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        result.GetProperty("success").GetBoolean().Should().BeFalse();
        result.GetProperty("message").GetString().Should().Contain("Connection failed");
    }

    [Fact]
    public async Task TestConnection_WithMalformedUrl_ShouldReturn400()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        
        var testRequest = new
        {
            immichUrl = "malformed-url-format",
            immichApiKey = "test-api-key"
        };

        // Act
        var response = await _client.PostAsync("/api/config/test",
            new StringContent(JsonSerializer.Serialize(testRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid URL format");
    }

    #endregion

    #region Resize Profiles CRUD Tests

    [Fact]
    public async Task CreateProfile_WithValidData_ShouldPersistToDatabase()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        
        var profileRequest = new
        {
            name = "Medium Resolution",
            width = 1920,
            height = 1080,
            quality = 85
        };

        // Act
        var response = await _client.PostAsync("/api/profiles",
            new StringContent(JsonSerializer.Serialize(profileRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        var profileId = result.GetProperty("id").GetInt32();
        
        // Verify persisted to database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var profile = await context.ResizeProfiles.FindAsync(profileId);
        
        profile.Should().NotBeNull();
        profile!.Name.Should().Be("Medium Resolution");
        profile.Width.Should().Be(1920);
        profile.Height.Should().Be(1080);
        profile.Quality.Should().Be(85);
    }

    [Fact]
    public async Task CreateProfile_WithInvalidDimensions_ShouldReturn400()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        
        var profileRequest = new
        {
            name = "Invalid Profile",
            width = 0, // Invalid
            height = -100, // Invalid
            quality = 85
        };

        // Act
        var response = await _client.PostAsync("/api/profiles",
            new StringContent(JsonSerializer.Serialize(profileRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Width must be greater than 0");
    }

    [Fact]
    public async Task UpdateProfile_WithValidChanges_ShouldUpdateDatabase()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        var profileId = await CreateTestProfileAsync();
        
        var updateRequest = new
        {
            name = "Updated Profile Name",
            width = 2560,
            height = 1440,
            quality = 90
        };

        // Act
        var response = await _client.PutAsync($"/api/profiles/{profileId}",
            new StringContent(JsonSerializer.Serialize(updateRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify updated in database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var profile = await context.ResizeProfiles.FindAsync(profileId);
        
        profile.Should().NotBeNull();
        profile!.Name.Should().Be("Updated Profile Name");
        profile.Width.Should().Be(2560);
        profile.Height.Should().Be(1440);
        profile.Quality.Should().Be(90);
    }

    [Fact]
    public async Task UpdateProfile_WithNonExistentId_ShouldReturn404()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        
        var updateRequest = new
        {
            name = "Non-existent Profile",
            width = 1920,
            height = 1080,
            quality = 85
        };

        // Act
        var response = await _client.PutAsync("/api/profiles/99999",
            new StringContent(JsonSerializer.Serialize(updateRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Profile not found");
    }

    [Fact]
    public async Task DeleteProfile_WithExistingId_ShouldRemoveFromDatabase()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        var profileId = await CreateTestProfileAsync();

        // Verify profile exists
        using var scope1 = _factory.Services.CreateScope();
        var context1 = scope1.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var profileBefore = await context1.ResizeProfiles.FindAsync(profileId);
        profileBefore.Should().NotBeNull();

        // Act
        var response = await _client.DeleteAsync($"/api/profiles/{profileId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify removed from database
        using var scope2 = _factory.Services.CreateScope();
        var context2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var profileAfter = await context2.ResizeProfiles.FindAsync(profileId);
        profileAfter.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProfile_WithNonExistentId_ShouldReturn404()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();

        // Act
        var response = await _client.DeleteAsync("/api/profiles/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Profile not found");
    }

    [Fact]
    public async Task DeleteProfile_WhenInUse_ShouldReturn409()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        var profileId = await CreateTestProfileAsync();
        
        // Create a task that uses this profile
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var task = new BackgroundTask
        {
            Id = "test-task",
            Type = "Resize",
            Status = "InProgress",
            UserId = 1,
            CreatedAt = DateTime.UtcNow,
            ProfileId = profileId
        };
        await context.Tasks.AddAsync(task);
        await context.SaveChangesAsync();

        // Act
        var response = await _client.DeleteAsync($"/api/profiles/{profileId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Profile is currently in use");
    }

    #endregion

    #region Authentication Tests

    [Fact]
    public async Task ConfigEndpoints_WithoutAuthentication_ShouldReturn401()
    {
        // Act & Assert
        var getResponse = await _client.GetAsync("/api/config");
        getResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var postResponse = await _client.PostAsync("/api/config", 
            new StringContent("{}", Encoding.UTF8, "application/json"));
        postResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var testResponse = await _client.PostAsync("/api/config/test", 
            new StringContent("{}", Encoding.UTF8, "application/json"));
        testResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProfileEndpoints_WithoutAuthentication_ShouldReturn401()
    {
        // Act & Assert
        var postResponse = await _client.PostAsync("/api/profiles", 
            new StringContent("{}", Encoding.UTF8, "application/json"));
        postResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var putResponse = await _client.PutAsync("/api/profiles/1", 
            new StringContent("{}", Encoding.UTF8, "application/json"));
        putResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var deleteResponse = await _client.DeleteAsync("/api/profiles/1");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Helper Methods

    private async Task SetupAuthenticatedClientAsync()
    {
        // Create test user and get auth token
        await CreateTestUserAsync();
        var token = await GetAuthTokenAsync();
        
        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task CreateTestUserAsync()
    {
        var registerRequest = new
        {
            username = "testuser",
            password = "TestPassword123!"
        };

        var response = await _client.PostAsync("/api/auth/register",
            new StringContent(JsonSerializer.Serialize(registerRequest), Encoding.UTF8, "application/json"));
        
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var loginRequest = new
        {
            username = "testuser",
            password = "TestPassword123!"
        };

        var loginResponse = await _client.PostAsync("/api/auth/login",
            new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json"));

        loginResponse.EnsureSuccessStatusCode();
        
        var loginContent = await loginResponse.Content.ReadAsStringAsync();
        var loginResult = JsonSerializer.Deserialize<JsonElement>(loginContent);
        
        return loginResult.GetProperty("access_token").GetString()!;
    }

    private async Task SeedConfigurationAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        await context.AppSettings.AddRangeAsync(
            new AppSetting { Key = "Immich:Url", Value = "https://demo.immich.app" },
            new AppSetting { Key = "Immich:ApiKey", Value = "test-api-key-123" }
        );
        
        await context.SaveChangesAsync();
    }

    private async Task<int> CreateTestProfileAsync()
    {
        var profileRequest = new
        {
            name = "Test Profile",
            width = 1920,
            height = 1080,
            quality = 85
        };

        var response = await _client.PostAsync("/api/profiles",
            new StringContent(JsonSerializer.Serialize(profileRequest), Encoding.UTF8, "application/json"));

        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        
        return result.GetProperty("id").GetInt32();
    }

    #endregion

    public void Dispose()
    {
        _mockImmichServer?.Dispose();
        _client?.Dispose();
    }
}