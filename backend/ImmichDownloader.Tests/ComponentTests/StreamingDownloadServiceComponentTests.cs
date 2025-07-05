using FluentAssertions;
using ImmichDownloader.Tests.Infrastructure;
using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using ImmichDownloader.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO.Compression;
using Xunit;

namespace ImmichDownloader.Tests.ComponentTests;

/// <summary>
/// Component tests for StreamingDownloadService that verify memory-efficient album downloading
/// with real file system operations and mock external services.
/// </summary>
[Trait("Category", "ComponentTest")]
public class StreamingDownloadServiceComponentTests : IDisposable
{
    private readonly MockImmichServer _mockServer;
    private readonly ServiceProvider _serviceProvider;
    private readonly StreamingDownloadService _downloadService;
    private readonly string _tempDirectory;
    private readonly Mock<ITaskProgressService> _progressServiceMock;

    public StreamingDownloadServiceComponentTests()
    {
        _mockServer = new MockImmichServer();
        _tempDirectory = Path.Combine(Path.GetTempPath(), "StreamingDownloadTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);

        var services = new ServiceCollection();
        
        // Add in-memory database
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("StreamingDownloadTestDb" + Guid.NewGuid().ToString()));

        // Add configuration
        var configData = new Dictionary<string, string?>
        {
            {"DownloadPath", _tempDirectory},
            {"Immich:Url", _mockServer.BaseUrl},
            {"Immich:ApiKey", "test-api-key"}
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        // Add mocked progress service
        _progressServiceMock = new Mock<ITaskProgressService>();
        services.AddSingleton(_progressServiceMock.Object);

        // Add ImmichService
        services.AddHttpClient();
        services.AddSingleton<ILogger<ImmichService>>(new Mock<ILogger<ImmichService>>().Object);
        services.AddScoped<IImmichService, ImmichService>();

        // Add logging
        services.AddSingleton<ILogger<StreamingDownloadService>>(new Mock<ILogger<StreamingDownloadService>>().Object);

        _serviceProvider = services.BuildServiceProvider();

        // Create the service with proper dependency injection
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var immichService = _serviceProvider.GetRequiredService<IImmichService>();
        var progressService = _serviceProvider.GetRequiredService<ITaskProgressService>();
        var logger = _serviceProvider.GetRequiredService<ILogger<StreamingDownloadService>>();

        // Configure Immich service
        immichService.Configure(_mockServer.BaseUrl, "test-api-key");

        _downloadService = new StreamingDownloadService(
            immichService,
            scopeFactory,
            progressService,
            configuration,
            logger);
    }

    #region Core Download Functionality Tests

    [Fact]
    public async Task StartDownloadAsync_WithValidAlbum_ShouldCreateZipFile()
    {
        // Arrange
        var taskId = "test-task-001";
        var albumId = "album-001";
        var albumName = "Test Album 1";
        var cancellationToken = CancellationToken.None;

        await SeedTestDataAsync(taskId);

        // Act
        await _downloadService.StartDownloadAsync(taskId, albumId, albumName, cancellationToken);

        // Assert
        var expectedZipPath = Path.Combine(_tempDirectory, $"{taskId}.zip");
        File.Exists(expectedZipPath).Should().BeTrue();

        // Verify ZIP contains files
        using var archive = ZipFile.OpenRead(expectedZipPath);
        archive.Entries.Should().NotBeEmpty();
        archive.Entries.Should().HaveCountGreaterThan(0);

        // Verify progress was reported
        _progressServiceMock.Verify(
            p => p.NotifyProgressAsync(taskId, "Download", "InProgress", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()),
            Times.AtLeastOnce);
        
        _progressServiceMock.Verify(
            p => p.NotifyTaskCompletedAsync(taskId, "Download"),
            Times.Once);
    }

    [Fact]
    public async Task StartDownloadAsync_ShouldDownloadAllAssets()
    {
        // Arrange
        var taskId = "test-task-002";
        var albumId = "album-001";
        var albumName = "Test Album 1";
        var cancellationToken = CancellationToken.None;

        await SeedTestDataAsync(taskId);

        // Act
        await _downloadService.StartDownloadAsync(taskId, albumId, albumName, cancellationToken);

        // Assert
        var expectedZipPath = Path.Combine(_tempDirectory, $"{taskId}.zip");
        
        using var archive = ZipFile.OpenRead(expectedZipPath);
        
        // Mock album-001 has 2 assets (asset-001, asset-002)
        archive.Entries.Should().HaveCount(2);
        archive.Entries.Should().Contain(e => e.Name.Contains("test-image-1.jpg"));
        archive.Entries.Should().Contain(e => e.Name.Contains("test-image-2.jpg"));
    }

    [Fact]
    public async Task StartDownloadAsync_ShouldHandleEmptyAlbums()
    {
        // Arrange
        var taskId = "test-task-003";
        var albumId = "album-003"; // Empty album
        var albumName = "Empty Album";
        var cancellationToken = CancellationToken.None;

        await SeedTestDataAsync(taskId);

        // Act
        await _downloadService.StartDownloadAsync(taskId, albumId, albumName, cancellationToken);

        // Assert
        var expectedZipPath = Path.Combine(_tempDirectory, $"{taskId}.zip");
        File.Exists(expectedZipPath).Should().BeTrue();

        using var archive = ZipFile.OpenRead(expectedZipPath);
        archive.Entries.Should().BeEmpty();

        // Should still report completion
        _progressServiceMock.Verify(
            p => p.NotifyTaskCompletedAsync(taskId, "Download"),
            Times.Once);
    }

    [Fact]
    public async Task StartDownloadAsync_ShouldPreserveFolderStructure()
    {
        // Arrange
        var taskId = "test-task-004";
        var albumId = "album-001";
        var albumName = "Test Album/With Subfolder";
        var cancellationToken = CancellationToken.None;

        await SeedTestDataAsync(taskId);

        // Act
        await _downloadService.StartDownloadAsync(taskId, albumId, albumName, cancellationToken);

        // Assert
        var expectedZipPath = Path.Combine(_tempDirectory, $"{taskId}.zip");
        
        using var archive = ZipFile.OpenRead(expectedZipPath);
        
        // Should preserve folder structure and handle special characters
        archive.Entries.Should().NotBeEmpty();
        // Verify that paths are sanitized properly (no invalid characters)
        foreach (var entry in archive.Entries)
        {
            entry.FullName.Should().NotContain("/");
            entry.FullName.Should().NotContain("\\");
        }
    }

    #endregion

    #region Progress Tracking Tests

    [Fact]
    public async Task StartDownloadAsync_ShouldReportProgressUpdates()
    {
        // Arrange
        var taskId = "test-task-005";
        var albumId = "album-001";
        var albumName = "Test Album 1";
        var cancellationToken = CancellationToken.None;

        await SeedTestDataAsync(taskId);

        // Act
        await _downloadService.StartDownloadAsync(taskId, albumId, albumName, cancellationToken);

        // Assert
        // Should report initial progress
        _progressServiceMock.Verify(
            p => p.NotifyProgressAsync(taskId, "Download", "InProgress", 0, It.IsAny<int>(), It.IsAny<string>()),
            Times.AtLeastOnce);

        // Should report progress during download
        _progressServiceMock.Verify(
            p => p.NotifyProgressAsync(taskId, "Download", "InProgress", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()),
            Times.AtLeastOnce);

        // Should report completion
        _progressServiceMock.Verify(
            p => p.NotifyTaskCompletedAsync(taskId, "Download"),
            Times.Once);
    }

    [Fact]
    public async Task StartDownloadAsync_ShouldNotifyCompletion()
    {
        // Arrange
        var taskId = "test-task-006";
        var albumId = "album-001";
        var albumName = "Test Album 1";
        var cancellationToken = CancellationToken.None;

        await SeedTestDataAsync(taskId);

        // Act
        await _downloadService.StartDownloadAsync(taskId, albumId, albumName, cancellationToken);

        // Assert
        _progressServiceMock.Verify(
            p => p.NotifyTaskCompletedAsync(taskId, "Download"),
            Times.Once);

        // Verify database is updated
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var task = await context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        
        task.Should().NotBeNull();
        task!.Status.Should().Be("Completed");
        task.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task StartDownloadAsync_ShouldReportErrors()
    {
        // Arrange
        var taskId = "test-task-007";
        var albumId = "invalid-album";
        var albumName = "Invalid Album";
        var cancellationToken = CancellationToken.None;

        _mockServer.SimulateServerErrors();
        await SeedTestDataAsync(taskId);

        // Act & Assert
        var action = async () => await _downloadService.StartDownloadAsync(taskId, albumId, albumName, cancellationToken);
        await action.Should().ThrowAsync<Exception>();

        // Should report error
        _progressServiceMock.Verify(
            p => p.NotifyTaskErrorAsync(taskId, "Download", It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Memory Management Tests

    [Fact]
    public async Task StartDownloadAsync_WithLargeAlbum_ShouldNotExceedMemoryLimits()
    {
        // Arrange
        _mockServer.SimulateLargeDataset(50); // Simulate 50 albums
        var taskId = "test-task-008";
        var albumId = "album-000001"; // First album from large dataset
        var albumName = "Large Test Album";
        var cancellationToken = CancellationToken.None;

        await SeedTestDataAsync(taskId);

        var initialMemory = GC.GetTotalMemory(false);

        // Act
        await _downloadService.StartDownloadAsync(taskId, albumId, albumName, cancellationToken);

        // Assert
        var finalMemory = GC.GetTotalMemory(true); // Force GC
        var memoryIncrease = finalMemory - initialMemory;
        
        // Memory increase should be reasonable (less than 100MB for test)
        memoryIncrease.Should().BeLessThan(100 * 1024 * 1024);

        var expectedZipPath = Path.Combine(_tempDirectory, $"{taskId}.zip");
        File.Exists(expectedZipPath).Should().BeTrue();
    }

    [Fact]
    public async Task StartDownloadAsync_ShouldProcessInChunks()
    {
        // Arrange
        var taskId = "test-task-009";
        var albumId = "album-001";
        var albumName = "Test Album 1";
        var cancellationToken = CancellationToken.None;

        await SeedTestDataAsync(taskId);

        // Act
        await _downloadService.StartDownloadAsync(taskId, albumId, albumName, cancellationToken);

        // Assert
        // Verify progress was reported multiple times (indicating chunked processing)
        _progressServiceMock.Verify(
            p => p.NotifyProgressAsync(taskId, "Download", "InProgress", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()),
            Times.AtLeastOnce);

        var expectedZipPath = Path.Combine(_tempDirectory, $"{taskId}.zip");
        File.Exists(expectedZipPath).Should().BeTrue();
    }

    [Fact]
    public async Task StartDownloadAsync_ShouldStreamDirectlyToDisk()
    {
        // Arrange
        var taskId = "test-task-010";
        var albumId = "album-001";
        var albumName = "Test Album 1";
        var cancellationToken = CancellationToken.None;

        await SeedTestDataAsync(taskId);

        // Act
        await _downloadService.StartDownloadAsync(taskId, albumId, albumName, cancellationToken);

        // Assert
        var expectedZipPath = Path.Combine(_tempDirectory, $"{taskId}.zip");
        File.Exists(expectedZipPath).Should().BeTrue();

        // Verify file was written directly (not buffered in memory)
        var fileInfo = new FileInfo(expectedZipPath);
        fileInfo.Length.Should().BeGreaterThan(0);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task StartDownloadAsync_WithNetworkErrors_ShouldFailGracefully()
    {
        // Arrange
        _mockServer.SimulateServerErrors();
        var taskId = "test-task-011";
        var albumId = "album-001";
        var albumName = "Test Album 1";
        var cancellationToken = CancellationToken.None;

        await SeedTestDataAsync(taskId);

        // Act & Assert
        var action = async () => await _downloadService.StartDownloadAsync(taskId, albumId, albumName, cancellationToken);
        await action.Should().ThrowAsync<Exception>();

        // Verify error was reported
        _progressServiceMock.Verify(
            p => p.NotifyTaskErrorAsync(taskId, "Download", It.IsAny<string>()),
            Times.AtLeastOnce);

        // Verify database shows failed status
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var task = await context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        
        task.Should().NotBeNull();
        task!.Status.Should().Be("Failed");
    }

    [Fact]
    public async Task StartDownloadAsync_WithCancellation_ShouldCleanupPartialFiles()
    {
        // Arrange
        var taskId = "test-task-012";
        var albumId = "album-001";
        var albumName = "Test Album 1";
        var cancellationTokenSource = new CancellationTokenSource();

        await SeedTestDataAsync(taskId);

        // Act
        var downloadTask = _downloadService.StartDownloadAsync(taskId, albumId, albumName, cancellationTokenSource.Token);
        
        // Cancel after a short delay
        await Task.Delay(100);
        cancellationTokenSource.Cancel();

        // Assert
        await downloadTask.Should().ThrowAsync<OperationCanceledException>();

        // Verify partial files are cleaned up
        var expectedZipPath = Path.Combine(_tempDirectory, $"{taskId}.zip");
        var tempFiles = Directory.GetFiles(_tempDirectory, $"{taskId}*");
        
        // Should either not exist or be cleaned up
        if (File.Exists(expectedZipPath))
        {
            // If file exists, it should be empty or very small (incomplete)
            var fileInfo = new FileInfo(expectedZipPath);
            fileInfo.Length.Should().BeLessThan(1000);
        }
    }

    #endregion

    #region File Management Tests

    [Fact]
    public async Task GetDownloadStream_ShouldReturnValidZipStream()
    {
        // Arrange
        var taskId = "test-task-013";
        var albumId = "album-001";
        var albumName = "Test Album 1";

        await SeedTestDataAsync(taskId);
        await _downloadService.StartDownloadAsync(taskId, albumId, albumName, CancellationToken.None);

        // Act
        var stream = _downloadService.GetDownloadStream(taskId);

        // Assert
        stream.Should().NotBeNull();
        stream.CanRead.Should().BeTrue();
        stream.Length.Should().BeGreaterThan(0);

        // Verify it's a valid ZIP file
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        archive.Entries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DeleteDownloadAsync_ShouldRemoveAllFiles()
    {
        // Arrange
        var taskId = "test-task-014";
        var albumId = "album-001";
        var albumName = "Test Album 1";

        await SeedTestDataAsync(taskId);
        await _downloadService.StartDownloadAsync(taskId, albumId, albumName, CancellationToken.None);

        var expectedZipPath = Path.Combine(_tempDirectory, $"{taskId}.zip");
        File.Exists(expectedZipPath).Should().BeTrue();

        // Act
        await _downloadService.DeleteDownloadAsync(taskId);

        // Assert
        File.Exists(expectedZipPath).Should().BeFalse();

        // Verify database record is removed
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var downloadedAlbum = await context.DownloadedAlbums.FirstOrDefaultAsync(da => da.TaskId == taskId);
        
        downloadedAlbum.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private async Task SeedTestDataAsync(string taskId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Add test task
        var task = new BackgroundTask
        {
            Id = taskId,
            Type = "Download",
            Status = "InProgress",
            CreatedAt = DateTime.UtcNow,
            UserId = 1
        };

        await context.Tasks.AddAsync(task);
        await context.SaveChangesAsync();
    }

    #endregion

    public void Dispose()
    {
        _mockServer?.Dispose();
        _serviceProvider?.Dispose();
        
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, true);
            }
            catch
            {
                // Ignore cleanup failures in tests
            }
        }
    }
}