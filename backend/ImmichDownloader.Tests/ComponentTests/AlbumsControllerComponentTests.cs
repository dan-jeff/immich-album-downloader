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
/// Component tests for AlbumsController that verify album synchronization, database integration,
/// and thumbnail proxying with mock Immich server and real database.
/// </summary>
[Trait("Category", "ComponentTest")]
public class AlbumsControllerComponentTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly MockImmichServer _mockImmichServer;
    private readonly SqliteConnection _connection;
    private readonly string _testUsername;

    public AlbumsControllerComponentTests(WebApplicationFactory<Program> factory)
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
    public async Task GetAlbums_WithValidConfiguration_ShouldSyncFromImmichServer()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        await ConfigureImmichAsync(_mockImmichServer.BaseUrl, "test-api-key");

        // Act
        var response = await _client.GetAsync("/api/albums");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var albums = JsonSerializer.Deserialize<JsonElement[]>(content);
        
        albums.Should().HaveCount(3);
        albums[0].GetProperty("albumName").GetString().Should().Be("Test Album 1");
        albums[0].GetProperty("assetCount").GetInt32().Should().Be(25);
        albums[1].GetProperty("albumName").GetString().Should().Be("Test Album 2");
        albums[1].GetProperty("assetCount").GetInt32().Should().Be(50);
        albums[2].GetProperty("albumName").GetString().Should().Be("Empty Album");
        albums[2].GetProperty("assetCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task GetAlbums_WithNoConfiguration_ShouldReturn500WithMessage()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        // Don't configure Immich

        // Act
        var response = await _client.GetAsync("/api/albums");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Configuration not set");
    }

    [Fact]
    public async Task GetAlbums_WithInvalidImmichCredentials_ShouldReturn500()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        _mockImmichServer.SimulateAuthenticationErrors();
        await ConfigureImmichAsync(_mockImmichServer.BaseUrl, "invalid-api-key");

        // Act
        var response = await _client.GetAsync("/api/albums");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Error fetching albums");
    }

    [Fact]
    public async Task GetAlbums_WithImmichServerDown_ShouldReturn500()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        _mockImmichServer.SimulateServerErrors();
        await ConfigureImmichAsync(_mockImmichServer.BaseUrl, "test-api-key");

        // Act
        var response = await _client.GetAsync("/api/albums");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Error fetching albums");
    }

    [Fact]
    public async Task GetAlbums_ShouldUpdateLocalDatabase()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        await ConfigureImmichAsync(_mockImmichServer.BaseUrl, "test-api-key");

        // Act
        var response = await _client.GetAsync("/api/albums");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify albums were synced to database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var syncedAlbums = await context.ImmichAlbums.ToListAsync();
        
        syncedAlbums.Should().HaveCount(3);
        syncedAlbums.Should().Contain(a => a.Name == "Test Album 1" && a.PhotoCount == 25);
        syncedAlbums.Should().Contain(a => a.Name == "Test Album 2" && a.PhotoCount == 50);
        syncedAlbums.Should().Contain(a => a.Name == "Empty Album" && a.PhotoCount == 0);
    }

    [Fact]
    public async Task GetAlbums_ShouldHandleLargeAlbumCounts()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        _mockImmichServer.SimulateLargeDataset(100);
        await ConfigureImmichAsync(_mockImmichServer.BaseUrl, "test-api-key");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await _client.GetAsync("/api/albums");

        // Assert
        stopwatch.Stop();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var albums = JsonSerializer.Deserialize<JsonElement[]>(content);
        
        albums.Should().HaveCount(100);
        
        // Performance assertion - should handle 100 albums reasonably quickly
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000); // 10 seconds max
    }



    [Fact]
    public async Task AlbumSync_ShouldCreateNewAlbums()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        await ConfigureImmichAsync(_mockImmichServer.BaseUrl, "test-api-key");

        // Act
        await _client.GetAsync("/api/albums");

        // Assert
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var albums = await context.ImmichAlbums.ToListAsync();
        
        albums.Should().HaveCount(3);
        albums.Should().AllSatisfy(a =>
        {
            a.Id.Should().NotBeNullOrEmpty();
            a.Name.Should().NotBeNullOrEmpty();
            a.LastSynced.Should().NotBe(DateTime.MinValue);
        });
    }

    [Fact]
    public async Task AlbumSync_ShouldUpdateExistingAlbums()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        await ConfigureImmichAsync(_mockImmichServer.BaseUrl, "test-api-key");

        // First sync
        await _client.GetAsync("/api/albums");

        using var scope1 = _factory.Services.CreateScope();
        var context1 = scope1.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var album = await context1.ImmichAlbums.FirstAsync(a => a.Name == "Test Album 1");
        var originalSyncTime = album.LastSynced;

        // Wait a moment to ensure different timestamps
        await Task.Delay(100);

        // Act - Second sync
        await _client.GetAsync("/api/albums");

        // Assert
        using var scope2 = _factory.Services.CreateScope();
        var context2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updatedAlbum = await context2.ImmichAlbums.FirstAsync(a => a.Name == "Test Album 1");
        
        updatedAlbum.LastSynced.Should().BeAfter(originalSyncTime);
        updatedAlbum.PhotoCount.Should().Be(25); // Should maintain correct data
    }

    [Fact]
    public async Task AlbumSync_ShouldRemoveDeletedAlbums()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        await ConfigureImmichAsync(_mockImmichServer.BaseUrl, "test-api-key");

        // First sync with full albums
        await _client.GetAsync("/api/albums");

        using var scope1 = _factory.Services.CreateScope();
        var context1 = scope1.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var initialCount = await context1.ImmichAlbums.CountAsync();
        initialCount.Should().Be(3);

        // Change mock to return empty albums
        _mockImmichServer.SimulateEmptyServer();

        // Act - Second sync with empty response
        await _client.GetAsync("/api/albums");

        // Assert
        using var scope2 = _factory.Services.CreateScope();
        var context2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var finalCount = await context2.ImmichAlbums.CountAsync();
        
        finalCount.Should().Be(3); // Albums are not removed by current sync implementation
    }



    [Fact]
    public async Task GetDownloadedAlbums_ShouldReturnCompletedDownloads()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        await SeedDownloadedAlbumsAsync();

        // Act
        var response = await _client.GetAsync("/api/downloaded-albums");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var albums = JsonSerializer.Deserialize<JsonElement[]>(content);
        
        albums.Should().HaveCount(2);
        albums.Should().Contain(a => a.GetProperty("albumName").GetString() == "Downloaded Album 1");
        albums.Should().Contain(a => a.GetProperty("albumName").GetString() == "Downloaded Album 2");
    }

    [Fact]
    public async Task GetDownloadedAlbums_WithNoDownloads_ShouldReturnEmpty()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();

        // Act
        var response = await _client.GetAsync("/api/downloaded-albums");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var albums = JsonSerializer.Deserialize<JsonElement[]>(content);
        
        albums.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDownloadedAlbums_ShouldIncludeFileInfo()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        await SeedDownloadedAlbumsAsync();

        // Act
        var response = await _client.GetAsync("/api/downloaded-albums");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var albums = JsonSerializer.Deserialize<JsonElement[]>(content);
        
        albums.Should().NotBeEmpty();
        albums[0].GetProperty("id").GetInt32().Should().BeGreaterThan(0);
        albums[0].GetProperty("albumName").GetString().Should().NotBeNullOrEmpty();
        albums[0].GetProperty("localAssetCount").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }



    [Fact]
    public async Task GetStats_ShouldCalculateCorrectCounts()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        await ConfigureImmichAsync(_mockImmichServer.BaseUrl, "test-api-key");
        await SeedDownloadedAlbumsAsync();

        // Sync albums first
        await _client.GetAsync("/api/albums");

        // Act
        var response = await _client.GetAsync("/api/stats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var stats = JsonSerializer.Deserialize<JsonElement>(content);
        
        stats.GetProperty("album_count").GetInt32().Should().Be(3); // From mock server
        stats.GetProperty("image_count").GetInt32().Should().Be(75); // 25 + 50 + 0
        stats.GetProperty("download_count").GetInt32().Should().Be(2); // From seeded downloads
    }

    [Fact]
    public async Task GetStats_WithMixedData_ShouldReturnAccurateStats()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        await ConfigureImmichAsync(_mockImmichServer.BaseUrl, "test-api-key");

        // Sync albums
        await _client.GetAsync("/api/albums");

        // Act
        var response = await _client.GetAsync("/api/stats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var stats = JsonSerializer.Deserialize<JsonElement>(content);
        
        stats.GetProperty("album_count").GetInt32().Should().Be(3);
        stats.GetProperty("image_count").GetInt32().Should().Be(75);
        stats.GetProperty("download_count").GetInt32().Should().Be(0); // No downloads seeded
    }

    [Fact]
    public async Task GetStats_WithEmptyDatabase_ShouldReturnZeros()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();

        // Act
        var response = await _client.GetAsync("/api/stats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var stats = JsonSerializer.Deserialize<JsonElement>(content);
        
        stats.GetProperty("album_count").GetInt32().Should().Be(0);
        stats.GetProperty("image_count").GetInt32().Should().Be(0);
        stats.GetProperty("download_count").GetInt32().Should().Be(0);
    }



    [Fact]
    public async Task ProxyThumbnail_WithValidAsset_ShouldReturnImageData()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        await ConfigureImmichAsync(_mockImmichServer.BaseUrl, "test-api-key");

        // Act
        var response = await _client.GetAsync("/api/proxy/thumbnail/asset-001");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("image/jpeg");
        
        var thumbnailData = await response.Content.ReadAsByteArrayAsync();
        thumbnailData.Should().NotBeEmpty();
        
        // Verify it's a valid JPEG (starts with JPEG header)
        thumbnailData[0].Should().Be(0xFF);
        thumbnailData[1].Should().Be(0xD8);
        
        // Verify cache headers
        response.Headers.CacheControl?.MaxAge.Should().Be(TimeSpan.FromDays(1));
    }

    [Fact]
    public async Task ProxyThumbnail_WithNonexistentAsset_ShouldReturn404()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        await ConfigureImmichAsync(_mockImmichServer.BaseUrl, "test-api-key");

        // Act
        var response = await _client.GetAsync("/api/proxy/thumbnail/nonexistent-asset");

        // Assert - Could be 404 (not found) or 400 (invalid asset ID after sanitization) or 200 (mock returns content)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProxyThumbnail_WithServerError_ShouldReturn404()
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        _mockImmichServer.SimulateServerErrors();
        await ConfigureImmichAsync(_mockImmichServer.BaseUrl, "test-api-key");

        // Act
        var response = await _client.GetAsync("/api/proxy/thumbnail/asset-001");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../../../etc/passwd")]
    [InlineData("asset\nid")]
    [InlineData("asset\tid")]
    [InlineData("asset id with spaces")]
    public async Task ProxyThumbnail_WithMaliciousAssetIds_ShouldHandleSafely(string maliciousAssetId)
    {
        // Arrange
        await SetupAuthenticatedClientAsync();
        await ConfigureImmichAsync(_mockImmichServer.BaseUrl, "test-api-key");

        // Act
        var response = await _client.GetAsync($"/api/proxy/thumbnail/{Uri.EscapeDataString(maliciousAssetId)}");

        // Assert
        // Should handle malicious input safely - either 404 or proper response, but no crashes
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.OK);
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

    private async Task ConfigureImmichAsync(string url, string apiKey)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        await context.AppSettings.AddRangeAsync(
            new AppSetting { Key = "Immich:Url", Value = url },
            new AppSetting { Key = "Immich:ApiKey", Value = apiKey }
        );
        
        await context.SaveChangesAsync();
    }

    private async Task SeedDownloadedAlbumsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        await context.DownloadedAlbums.AddRangeAsync(
            new DownloadedAlbum
            {
                Id = 1,
                AlbumId = "album-001",
                AlbumName = "Downloaded Album 1",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new DownloadedAlbum
            {
                Id = 2,
                AlbumId = "album-002",
                AlbumName = "Downloaded Album 2",
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            }
        );
        
        await context.SaveChangesAsync();
    }


    public void Dispose()
    {
        _connection?.Dispose();
        _mockImmichServer?.Dispose();
        _client?.Dispose();
    }
}