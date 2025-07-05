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
/// Component tests for AlbumsController that verify album synchronization, database integration,
/// and thumbnail proxying with mock Immich server and real database.
/// </summary>
[Trait("Category", "ComponentTest")]
public class AlbumsControllerComponentTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly MockImmichServer _mockImmichServer;

    public AlbumsControllerComponentTests(WebApplicationFactory<Program> factory)
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
                    options.UseInMemoryDatabase("AlbumsTestDb" + Guid.NewGuid().ToString());
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                });
            });
        });
        
        _client = _factory.CreateClient();
    }

    #region Album Synchronization Tests

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
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Immich is not configured");
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
        content.Should().Contain("Failed to retrieve albums");
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
        content.Should().Contain("Failed to retrieve albums");
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

    #endregion

    #region Database Integration Tests

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
            a.ImmichId.Should().NotBeNullOrEmpty();
            a.Name.Should().NotBeNullOrEmpty();
            a.LastSyncedAt.Should().NotBeNull();
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
        var originalSyncTime = album.LastSyncedAt;

        // Wait a moment to ensure different timestamps
        await Task.Delay(100);

        // Act - Second sync
        await _client.GetAsync("/api/albums");

        // Assert
        using var scope2 = _factory.Services.CreateScope();
        var context2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updatedAlbum = await context2.ImmichAlbums.FirstAsync(a => a.Name == "Test Album 1");
        
        updatedAlbum.LastSyncedAt.Should().BeAfter(originalSyncTime!.Value);
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
        
        finalCount.Should().Be(0); // All albums should be removed
    }

    #endregion

    #region Downloaded Albums Tests

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
        albums[0].GetProperty("downloadedAt").GetDateTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        albums[0].GetProperty("taskId").GetString().Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Statistics Tests

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

    #endregion

    #region Thumbnail Proxy Tests

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

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
                ImmichAlbumId = "album-001",
                AlbumName = "Downloaded Album 1",
                DownloadedAt = DateTime.UtcNow.AddDays(-1),
                TaskId = "task-001",
                UserId = 1
            },
            new DownloadedAlbum
            {
                Id = 2,
                ImmichAlbumId = "album-002",
                AlbumName = "Downloaded Album 2",
                DownloadedAt = DateTime.UtcNow.AddDays(-2),
                TaskId = "task-002",
                UserId = 1
            }
        );
        
        await context.SaveChangesAsync();
    }

    #endregion

    public void Dispose()
    {
        _mockImmichServer?.Dispose();
        _client?.Dispose();
    }
}