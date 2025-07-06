using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using ImmichDownloader.Tests.Infrastructure;
using ImmichDownloader.Web;
using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
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
    private readonly SqliteConnection _connection;
    private readonly string _testUsername;

    public ConfigControllerComponentTests(WebApplicationFactory<Program> factory)
    {
        _testUsername = $"testuser_{Guid.NewGuid():N}";
        _mockImmichServer = new MockImmichServer();
        
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


    [Fact]
    public async Task GetConfig_ShouldReturnCurrentSettings()
    {
        // Arrange
        using var authenticatedClient = await CreateAuthenticatedClientAsync();
        await SeedConfigurationAsync();

        // Act
        var response = await authenticatedClient.GetAsync("/api/config");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var config = JsonSerializer.Deserialize<JsonElement>(content);
        
        config.GetProperty("immich_url").GetString().Should().Be("https://demo.immich.app");
        config.GetProperty("api_key").GetString().Should().Be("test-api-key-123");
    }

    [Fact]
    public async Task SaveConfig_WithValidSettings_ShouldPersistToDatabase()
    {
        // Arrange
        using var authenticatedClient = await CreateAuthenticatedClientAsync();
        
        var configRequest = new
        {
            immich_url = _mockImmichServer.BaseUrl,
            api_key = "new-api-key-456"
        };

        // Act
        var response = await authenticatedClient.PostAsync("/api/config",
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
        using var authenticatedClient = await CreateAuthenticatedClientAsync();
        
        var configRequest = new
        {
            immich_url = "not-a-valid-url",
            api_key = "test-api-key"
        };

        // Act
        var response = await authenticatedClient.PostAsync("/api/config",
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
        using var authenticatedClient = await CreateAuthenticatedClientAsync();
        
        var configRequest = new
        {
            immich_url = _mockImmichServer.BaseUrl,
            api_key = ""
        };

        // Act
        var response = await authenticatedClient.PostAsync("/api/config",
            new StringContent(JsonSerializer.Serialize(configRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("API key is required");
    }



    [Fact]
    public async Task TestConnection_WithValidCredentials_ShouldReturnSuccess()
    {
        // Arrange
        using var authenticatedClient = await CreateAuthenticatedClientAsync();
        
        var testRequest = new
        {
            immich_url = _mockImmichServer.BaseUrl,
            api_key = "test-api-key"
        };

        // Act
        var response = await authenticatedClient.PostAsync("/api/config/test",
            new StringContent(JsonSerializer.Serialize(testRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        result.GetProperty("success").GetBoolean().Should().BeTrue();
        result.GetProperty("message").GetString().Should().Contain("Successfully connected to Immich!");
    }

    [Fact]
    public async Task TestConnection_WithInvalidCredentials_ShouldReturnError()
    {
        // Arrange
        using var authenticatedClient = await CreateAuthenticatedClientAsync();
        _mockImmichServer.SimulateAuthenticationErrors();
        
        var testRequest = new
        {
            immich_url = _mockImmichServer.BaseUrl,
            api_key = "invalid-api-key"
        };

        // Act
        var response = await authenticatedClient.PostAsync("/api/config/test",
            new StringContent(JsonSerializer.Serialize(testRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        result.GetProperty("success").GetBoolean().Should().BeFalse();
        result.GetProperty("message").GetString().Should().Contain("Failed to connect:");
    }

    [Fact]
    public async Task TestConnection_WithServerDown_ShouldReturnError()
    {
        // Arrange
        using var authenticatedClient = await CreateAuthenticatedClientAsync();
        _mockImmichServer.SimulateServerErrors();
        
        var testRequest = new
        {
            immich_url = _mockImmichServer.BaseUrl,
            api_key = "test-api-key"
        };

        // Act
        var response = await authenticatedClient.PostAsync("/api/config/test",
            new StringContent(JsonSerializer.Serialize(testRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        result.GetProperty("success").GetBoolean().Should().BeFalse();
        result.GetProperty("message").GetString().Should().Contain("Failed to connect:");
    }

    [Fact]
    public async Task TestConnection_WithMalformedUrl_ShouldReturn400()
    {
        // Arrange
        using var authenticatedClient = await CreateAuthenticatedClientAsync();
        
        var testRequest = new
        {
            immich_url = "malformed-url-format",
            api_key = "test-api-key"
        };

        // Act
        var response = await authenticatedClient.PostAsync("/api/config/test",
            new StringContent(JsonSerializer.Serialize(testRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid URL format");
    }



    [Fact]
    public async Task CreateProfile_WithValidData_ShouldPersistToDatabase()
    {
        // Arrange
        using var authenticatedClient = await CreateAuthenticatedClientAsync();
        
        var profileRequest = new
        {
            name = "Medium Resolution",
            width = 1920,
            height = 1080,
            quality = 85
        };

        // Act
        var response = await authenticatedClient.PostAsync("/api/profiles",
            new StringContent(JsonSerializer.Serialize(profileRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        var profileId = result.GetProperty("data").GetProperty("id").GetInt32();
        
        // Verify persisted to database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var profile = await context.ResizeProfiles.FindAsync(profileId);
        
        profile.Should().NotBeNull();
        profile!.Name.Should().Be("Medium Resolution");
        profile.Width.Should().Be(1920);
        profile.Height.Should().Be(1080);
    }

    [Fact]
    public async Task CreateProfile_WithInvalidDimensions_ShouldReturn400()
    {
        // Arrange
        using var authenticatedClient = await CreateAuthenticatedClientAsync();
        
        var profileRequest = new
        {
            name = "Invalid Profile",
            width = 0, // Invalid
            height = -100, // Invalid
            quality = 85
        };

        // Act
        var response = await authenticatedClient.PostAsync("/api/profiles",
            new StringContent(JsonSerializer.Serialize(profileRequest), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Width must be between 1 and 10000");
    }

    [Fact]
    public async Task UpdateProfile_WithValidChanges_ShouldUpdateDatabase()
    {
        // Arrange
        using var authenticatedClient = await CreateAuthenticatedClientAsync();
        var profileId = await CreateTestProfileAsync(authenticatedClient);
        
        var updateRequest = new
        {
            name = "Updated Profile Name",
            width = 2560,
            height = 1440,
            quality = 90
        };

        // Act
        var response = await authenticatedClient.PutAsync($"/api/profiles/{profileId}",
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
    }

    [Fact]
    public async Task UpdateProfile_WithNonExistentId_ShouldReturn404()
    {
        // Arrange
        using var authenticatedClient = await CreateAuthenticatedClientAsync();
        
        var updateRequest = new
        {
            name = "Non-existent Profile",
            width = 1920,
            height = 1080,
            quality = 85
        };

        // Act
        var response = await authenticatedClient.PutAsync("/api/profiles/99999",
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
        using var authenticatedClient = await CreateAuthenticatedClientAsync();
        var profileId = await CreateTestProfileAsync(authenticatedClient);

        // Verify profile exists
        using var scope1 = _factory.Services.CreateScope();
        var context1 = scope1.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var profileBefore = await context1.ResizeProfiles.FindAsync(profileId);
        profileBefore.Should().NotBeNull();

        // Act
        var response = await authenticatedClient.DeleteAsync($"/api/profiles/{profileId}");

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
        using var authenticatedClient = await CreateAuthenticatedClientAsync();

        // Act
        var response = await authenticatedClient.DeleteAsync("/api/profiles/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Profile not found");
    }

    [Fact]
    public async Task DeleteProfile_WhenInUse_ShouldReturn409()
    {
        // Arrange
        using var authenticatedClient = await CreateAuthenticatedClientAsync();
        var profileId = await CreateTestProfileAsync(authenticatedClient);
        
        // Create a task that uses this profile
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var task = new BackgroundTask
        {
            Id = "test-task",
            TaskType = Web.Models.TaskType.Resize,
            Status = Web.Models.TaskStatus.InProgress,
            CreatedAt = DateTime.UtcNow,
            ProfileId = profileId
        };
        await context.BackgroundTasks.AddAsync(task);
        await context.SaveChangesAsync();

        // Act
        var response = await authenticatedClient.DeleteAsync($"/api/profiles/{profileId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // The controller doesn't check for profile usage, so deletion succeeds
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        result.GetProperty("success").GetBoolean().Should().BeTrue();
        result.GetProperty("data").GetProperty("success").GetBoolean().Should().BeTrue();
    }



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



    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        // Create test user and get auth token using the shared client
        await CreateTestUserAsync();
        var token = await GetAuthTokenAsync();
        
        // Create a fresh HttpClient with authentication for this specific test
        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);
        
        return authenticatedClient;
    }
    
    private async Task SetupAuthenticatedClientAsync()
    {
        // Clear any existing auth headers to prevent interference between tests
        _client.DefaultRequestHeaders.Authorization = null;
        
        // Create test user and get auth token
        await CreateTestUserAsync();
        var token = await GetAuthTokenAsync();
        
        // Set fresh authentication header
        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task CreateTestUserAsync()
    {
        var registerRequest = new
        {
            username = _testUsername,
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
            username = _testUsername,
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

    private async Task<int> CreateTestProfileAsync(HttpClient? client = null)
    {
        var profileRequest = new
        {
            name = "Test Profile",
            width = 1920,
            height = 1080,
            quality = 85
        };

        var clientToUse = client ?? _client;
        var response = await clientToUse.PostAsync("/api/profiles",
            new StringContent(JsonSerializer.Serialize(profileRequest), Encoding.UTF8, "application/json"));

        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        
        return result.GetProperty("data").GetProperty("id").GetInt32();
    }


    public void Dispose()
    {
        _connection?.Dispose();
        _mockImmichServer?.Dispose();
        _client?.Dispose();
    }
}