using FluentAssertions;
using ImmichDownloader.Tests.Infrastructure;
using ImmichDownloader.Web.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Net;

namespace ImmichDownloader.Tests.ComponentTests;

/// <summary>
/// Component tests for ImmichService that verify external API integration
/// using mock HTTP servers to simulate various Immich server scenarios.
/// </summary>
[Trait("Category", "ComponentTest")]
public class ImmichServiceComponentTests : IDisposable
{
    private readonly MockImmichServer _mockServer;
    private readonly ImmichService _immichService;
    private readonly Mock<ILogger<ImmichService>> _loggerMock;
    private readonly HttpClient _httpClient;

    public ImmichServiceComponentTests()
    {
        _mockServer = new MockImmichServer();
        _httpClient = new HttpClient();
        _loggerMock = new Mock<ILogger<ImmichService>>();
        
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);
        
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);
        
        _immichService = new ImmichService(httpClientFactory.Object, _loggerMock.Object, loggerFactory.Object);
    }

    #region Configuration Tests

    [Fact]
    public void Configure_WithValidCredentials_ShouldSetupCorrectly()
    {
        // Arrange
        var url = _mockServer.BaseUrl;
        var apiKey = "test-api-key";

        // Act & Assert - Should not throw
        var action = () => _immichService.Configure(url, apiKey);
        action.Should().NotThrow();
    }

    [Fact]
    public void Configure_WithInvalidUrl_ShouldThrowException()
    {
        // Arrange
        var invalidUrl = "not-a-valid-url";
        var apiKey = "test-api-key";

        // Act & Assert
        var action = () => _immichService.Configure(invalidUrl, apiKey);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid URL format*");
    }

    [Fact]
    public void Configure_WithEmptyApiKey_ShouldThrowException()
    {
        // Arrange
        var url = _mockServer.BaseUrl;
        var emptyApiKey = "";

        // Act & Assert
        var action = () => _immichService.Configure(url, emptyApiKey);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*API key cannot be empty*");
    }

    [Fact]
    public void Configure_WithNullApiKey_ShouldThrowException()
    {
        // Arrange
        var url = _mockServer.BaseUrl;
        string? nullApiKey = null;

        // Act & Assert
        var action = () => _immichService.Configure(url, nullApiKey!);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*API key cannot be empty*");
    }

    #endregion

    #region Connection Validation Tests

    [Fact]
    public async Task ValidateConnectionAsync_WithValidServer_ShouldReturnTrue()
    {
        // Arrange
        _immichService.Configure(_mockServer.BaseUrl, "test-api-key");

        // Act
        var result = await _immichService.ValidateConnectionAsync(_mockServer.BaseUrl, "test-api-key");

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithInvalidCredentials_ShouldReturnFalse()
    {
        // Arrange
        _mockServer.SimulateAuthenticationErrors();
        
        // Act
        var result = await _immichService.ValidateConnectionAsync(_mockServer.BaseUrl, "invalid-api-key");

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithServerDown_ShouldReturnFalse()
    {
        // Arrange
        _mockServer.SimulateServerErrors();
        
        // Act
        var result = await _immichService.ValidateConnectionAsync(_mockServer.BaseUrl, "test-api-key");

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithTimeout_ShouldReturnFalse()
    {
        // Arrange
        _mockServer.SimulateSlowServer(30000); // 30 second delay
        _httpClient.Timeout = TimeSpan.FromSeconds(2); // 2 second timeout
        
        // Act
        var result = await _immichService.ValidateConnectionAsync(_mockServer.BaseUrl, "test-api-key");

        // Assert
        result.Success.Should().BeFalse();
    }

    #endregion

    #region Album Operations Tests

    [Fact]
    public async Task GetAlbumsAsync_WithValidResponse_ShouldReturnAlbums()
    {
        // Arrange
        _immichService.Configure(_mockServer.BaseUrl, "test-api-key");

        // Act
        var result = await _immichService.GetAlbumsAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.Albums.Should().NotBeNull();
        result.Albums.Should().HaveCount(3);
        
        var albums = result.Albums!.ToList();
        albums[0].AlbumName.Should().Be("Test Album 1");
        albums[0].AssetCount.Should().Be(25);
        albums[1].AlbumName.Should().Be("Test Album 2");
        albums[1].AssetCount.Should().Be(50);
        albums[2].AlbumName.Should().Be("Empty Album");
        albums[2].AssetCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAlbumsAsync_WithEmptyResponse_ShouldReturnEmptyList()
    {
        // Arrange
        _mockServer.SimulateEmptyServer();
        _immichService.Configure(_mockServer.BaseUrl, "test-api-key");

        // Act
        var result = await _immichService.GetAlbumsAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.Albums.Should().NotBeNull();
        result.Albums.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAlbumsAsync_WithMalformedResponse_ShouldReturnError()
    {
        // Arrange
        _mockServer.SimulateServerErrors();
        _immichService.Configure(_mockServer.BaseUrl, "test-api-key");

        // Act
        var result = await _immichService.GetAlbumsAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        result.Albums.Should().BeNull();
    }

    [Fact]
    public async Task GetAlbumInfoAsync_WithValidId_ShouldReturnAlbumDetails()
    {
        // Arrange
        _immichService.Configure(_mockServer.BaseUrl, "test-api-key");

        // Act
        var result = await _immichService.GetAlbumInfoAsync("album-001");

        // Assert
        result.Success.Should().BeTrue();
        result.Album.Should().NotBeNull();
        result.Album!.AlbumName.Should().Be("Test Album 1");
        result.Album.Assets.Should().NotBeNullOrEmpty();
        result.Album.Assets.Should().HaveCount(2); // Mock returns 2 sample assets
    }

    [Fact]
    public async Task GetAlbumInfoAsync_WithInvalidId_ShouldThrowException()
    {
        // Arrange
        _mockServer.SimulateServerErrors();
        _immichService.Configure(_mockServer.BaseUrl, "test-api-key");

        // Act & Assert
        var action = async () => await _immichService.GetAlbumInfoAsync("invalid-album-id");
        await action.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetAlbumsAsync_WithLargeDataset_ShouldHandleCorrectly()
    {
        // Arrange
        _mockServer.SimulateLargeDataset(100);
        _immichService.Configure(_mockServer.BaseUrl, "test-api-key");

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _immichService.GetAlbumsAsync();
        stopwatch.Stop();

        // Assert
        result.Success.Should().BeTrue();
        result.Albums.Should().HaveCount(100);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete within 5 seconds
    }

    #endregion

    #region Asset Download Tests

    [Fact]
    public async Task DownloadAssetAsync_WithValidAsset_ShouldReturnStream()
    {
        // Arrange
        _immichService.Configure(_mockServer.BaseUrl, "test-api-key");

        // Act
        var result = await _immichService.DownloadAssetAsync("asset-001");

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DownloadAssetAsync_WithInvalidAsset_ShouldThrowException()
    {
        // Arrange
        _mockServer.SimulateServerErrors();
        _immichService.Configure(_mockServer.BaseUrl, "test-api-key");

        // Act & Assert
        var action = async () => await _immichService.DownloadAssetAsync("invalid-asset-id");
        await action.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task DownloadAssetAsync_WithLargeAsset_ShouldStreamCorrectly()
    {
        // Arrange
        _immichService.Configure(_mockServer.BaseUrl, "test-api-key");

        // Act
        var result = await _immichService.DownloadAssetAsync("asset-001");

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Length.Should().BeGreaterThan(1000); // Mock returns 50KB
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ApiCalls_WithNetworkErrors_ShouldRetryAndFail()
    {
        // Arrange
        _mockServer.SimulateServerErrors();
        _immichService.Configure(_mockServer.BaseUrl, "test-api-key");

        // Act
        var result = await _immichService.GetAlbumsAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        result.Error.Should().Contain("server error");
    }

    [Fact]
    public async Task ApiCalls_WithAuthenticationErrors_ShouldReturnError()
    {
        // Arrange
        _mockServer.SimulateAuthenticationErrors();
        _immichService.Configure(_mockServer.BaseUrl, "test-api-key");

        // Act
        var result = await _immichService.GetAlbumsAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        result.Error.Should().Contain("authentication");
    }

    [Fact]
    public async Task ApiCalls_WithoutConfiguration_ShouldThrowException()
    {
        // Arrange - Don't configure the service

        // Act & Assert
        var action = async () => await _immichService.GetAlbumsAsync();
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Immich service is not configured*");
    }

    #endregion

    public void Dispose()
    {
        _mockServer?.Dispose();
        _httpClient?.Dispose();
    }
}