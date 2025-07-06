using System.IO.Compression;
using System.Collections.Concurrent;
using ImmichDownloader.Web.Models;
using ImmichDownloader.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using ImmichDownloader.Web.Hubs;

namespace ImmichDownloader.Web.Services;

/// <summary>
/// Service interface for streaming downloads that write directly to disk instead of memory.
/// </summary>
public interface IStreamingDownloadService
{
    /// <summary>
    /// Starts downloading an album's assets in streaming mode, writing directly to a ZIP file.
    /// </summary>
    /// <param name="taskId">The unique identifier for the download task.</param>
    /// <param name="albumId">The ID of the album to download.</param>
    /// <param name="albumName">The name of the album being downloaded.</param>
    /// <param name="cancellationToken">Token to cancel the download operation.</param>
    /// <returns>A task representing the asynchronous download operation.</returns>
    Task StartDownloadAsync(string taskId, string albumId, string albumName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a stream for reading a completed download.
    /// </summary>
    /// <param name="taskId">The ID of the completed download task.</param>
    /// <returns>A stream for reading the download, or null if not found.</returns>
    Stream? GetDownloadStream(string taskId);
    
    /// <summary>
    /// Deletes a completed download and its associated files.
    /// </summary>
    /// <param name="taskId">The ID of the download to delete.</param>
    /// <returns>True if the download was successfully deleted, false otherwise.</returns>
    Task<bool> DeleteDownloadAsync(string taskId);
}

/// <summary>
/// Service that handles album downloads by streaming images directly to disk as ZIP files.
/// This approach reduces memory usage compared to in-memory processing.
/// </summary>
public class StreamingDownloadService : IStreamingDownloadService
{
    private readonly ILogger<StreamingDownloadService> _logger;
    private readonly IImmichService _immichService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<ProgressHub> _hubContext;
    private readonly string _downloadsPath;
    
    private const int CHUNK_SIZE = 50; // Process 50 images at a time
    private const int MAX_CONCURRENT_DOWNLOADS = 5;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamingDownloadService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for logging operations and errors.</param>
    /// <param name="immichService">Service for communicating with Immich server.</param>
    /// <param name="scopeFactory">Factory for creating service scopes for database operations.</param>
    /// <param name="hubContext">SignalR hub context for progress notifications.</param>
    /// <param name="configuration">Configuration provider for accessing application settings.</param>
    public StreamingDownloadService(
        ILogger<StreamingDownloadService> logger,
        IImmichService immichService,
        IServiceScopeFactory scopeFactory,
        IHubContext<ProgressHub> hubContext,
        IConfiguration configuration)
    {
        _logger = logger;
        _immichService = immichService;
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _downloadsPath = Path.Combine(configuration.GetValue<string>("DataPath") ?? "data", "downloads");
        
        // Ensure downloads directory exists
        Directory.CreateDirectory(_downloadsPath);
    }

    /// <summary>
    /// Starts downloading an album's assets in streaming mode, processing images in chunks
    /// and writing them directly to a ZIP file to minimize memory usage.
    /// </summary>
    /// <param name="taskId">The unique identifier for the download task.</param>
    /// <param name="albumId">The ID of the album to download.</param>
    /// <param name="albumName">The name of the album being downloaded.</param>
    /// <param name="cancellationToken">Token to cancel the download operation.</param>
    /// <returns>A task representing the asynchronous download operation.</returns>
    public async Task StartDownloadAsync(string taskId, string albumId, string albumName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting streaming download for album {AlbumId}, task {TaskId}", albumId, taskId);
            await NotifyProgressAsync(taskId, Models.TaskStatus.InProgress, "Starting download...");

            // Configure Immich service with database settings
            var (immichUrl, apiKey) = await GetImmichSettingsAsync();
            if (string.IsNullOrEmpty(immichUrl) || string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("Immich configuration not found in database");
                await UpdateTaskAsync(taskId, Models.TaskStatus.Error, "Immich service not configured");
                return;
            }

            _immichService.Configure(immichUrl, apiKey);

            // Get album info
            var (success, albumInfo, error) = await _immichService.GetAlbumInfoAsync(albumId);
            if (!success || albumInfo == null)
            {
                _logger.LogError("Failed to get album info: {Error}", error);
                await UpdateTaskAsync(taskId, Models.TaskStatus.Error, error ?? "Unknown error");
                return;
            }

            var allPhotos = albumInfo.Assets.Where(a => a.Type == "IMAGE").ToList();
            
            // Check which assets are already downloaded
            var existingAssetIds = await GetExistingAssetIdsAsync(albumId);
            var photos = allPhotos.Where(a => !existingAssetIds.Contains(a.Id)).ToList();
            var toDownload = photos.Count;
            
            _logger.LogInformation("Album {AlbumId} has {ToDownload} new photos to download", albumId, toDownload);

            if (toDownload == 0)
            {
                // Create empty ZIP file for empty albums
                var emptyZipFilePath = Path.Combine(_downloadsPath, $"{taskId}.zip");
                using (var zipFileStream = new FileStream(emptyZipFilePath, FileMode.Create, FileAccess.Write))
                using (var zipArchive = new ZipArchive(zipFileStream, ZipArchiveMode.Create, false))
                {
                    // Create empty ZIP - no entries needed
                }
                
                // Save album metadata with 0 photos
                await SaveDownloadedAlbumAsync(albumId, albumName, 0, emptyZipFilePath);
                await UpdateTaskAsync(taskId, Models.TaskStatus.Completed, "All photos already downloaded", 0, 0);
                await NotifyProgressAsync(taskId, Models.TaskStatus.Completed, "Download complete!");
                
                _logger.LogInformation("Completed download for empty album {AlbumId}", albumId);
                return;
            }

            // Update task with total count
            await UpdateTaskAsync(taskId, Models.TaskStatus.InProgress, "Starting download...", 0, toDownload);

            // Create streaming ZIP file
            var zipFilePath = Path.Combine(_downloadsPath, $"{taskId}.zip");
            var downloadedCount = 0;

            using (var zipFileStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write))
            using (var zipArchive = new ZipArchive(zipFileStream, ZipArchiveMode.Create, false))
            {
                // Process photos in chunks to limit memory usage
                for (int chunkStart = 0; chunkStart < toDownload; chunkStart += CHUNK_SIZE)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var chunkPhotos = photos.Skip(chunkStart).Take(CHUNK_SIZE).ToList();
                    var semaphore = new SemaphoreSlim(MAX_CONCURRENT_DOWNLOADS);

                    // Download chunk in parallel
                    var downloadTasks = chunkPhotos.Select(async photo =>
                    {
                        await semaphore.WaitAsync(cancellationToken);
                        try
                        {
                            var (downloadSuccess, data, downloadError) = await _immichService.DownloadAssetAsync(photo.Id);
                            if (downloadSuccess && data != null)
                            {
                                // Write directly to ZIP stream to avoid memory accumulation
                                lock (zipArchive)
                                {
                                    var entry = zipArchive.CreateEntry(photo.OriginalFileName);
                                    using var entryStream = entry.Open();
                                    entryStream.Write(data);
                                }
                                
                                // Record downloaded asset
                                await RecordDownloadedAssetAsync(albumId, photo.Id);
                                Interlocked.Increment(ref downloadedCount);
                                
                                // Update progress every 10 downloads
                                if (downloadedCount % 10 == 0)
                                {
                                    await UpdateTaskAsync(taskId, Models.TaskStatus.InProgress, 
                                        $"Downloaded {downloadedCount}/{toDownload} photos", downloadedCount, toDownload);
                                    await NotifyProgressAsync(taskId, Models.TaskStatus.InProgress, 
                                        $"Downloaded {downloadedCount}/{toDownload} photos", downloadedCount, toDownload);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Failed to download photo {PhotoId}: {Error}", photo.Id, downloadError);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error downloading photo {PhotoId}", photo.Id);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    await Task.WhenAll(downloadTasks);
                }
            }

            // Save album metadata
            await SaveDownloadedAlbumAsync(albumId, albumName, downloadedCount, zipFilePath);

            // Final update
            await UpdateTaskAsync(taskId, Models.TaskStatus.Completed, "Download complete!", downloadedCount, toDownload);
            await NotifyProgressAsync(taskId, Models.TaskStatus.Completed, "Download complete!");

            _logger.LogInformation("Completed streaming download for album {AlbumId}, downloaded {Count} photos", 
                albumId, downloadedCount);
        }
        catch (OperationCanceledException)
        {
            await UpdateTaskAsync(taskId, Models.TaskStatus.Error, "Download cancelled");
            await CleanupDownloadAsync(taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in streaming download task {TaskId}", taskId);
            await UpdateTaskAsync(taskId, Models.TaskStatus.Error, $"Error: {ex.Message}");
            await CleanupDownloadAsync(taskId);
        }
    }

    /// <summary>
    /// Gets a stream for reading a completed download ZIP file.
    /// Returns null if the download file is not found or cannot be opened.
    /// </summary>
    /// <param name="taskId">The ID of the completed download task.</param>
    /// <returns>A stream for reading the download, or null if not found.</returns>
    public Stream? GetDownloadStream(string taskId)
    {
        var zipFilePath = Path.Combine(_downloadsPath, $"{taskId}.zip");
        
        if (!File.Exists(zipFilePath))
        {
            _logger.LogWarning("ZIP file not found for task {TaskId}", taskId);
            return null;
        }

        try
        {
            // Return a file stream that can be streamed to the client
            return new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening download stream for task {TaskId}", taskId);
            return null;
        }
    }

    /// <summary>
    /// Deletes a completed download by removing its database record and associated ZIP file.
    /// This operation cannot be undone.
    /// </summary>
    /// <param name="taskId">The ID of the download to delete.</param>
    /// <returns>True if the download was successfully deleted, false otherwise.</returns>
    public async Task<bool> DeleteDownloadAsync(string taskId)
    {
        try
        {
            // Remove from database
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var task = await context.BackgroundTasks.FindAsync(taskId);
            if (task != null)
            {
                context.BackgroundTasks.Remove(task);
                await context.SaveChangesAsync();
            }

            // Delete ZIP file
            await CleanupDownloadAsync(taskId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting download {TaskId}", taskId);
            return false;
        }
    }

    private async Task<HashSet<string>> GetExistingAssetIdsAsync(string albumId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        return (await context.DownloadedAssets
            .Where(da => da.AlbumId == albumId)
            .Select(da => da.AssetId)
            .ToListAsync()).ToHashSet();
    }

    private async Task RecordDownloadedAssetAsync(string albumId, string assetId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Check if already exists to avoid duplicates
            var exists = await context.DownloadedAssets
                .AnyAsync(da => da.AlbumId == albumId && da.AssetId == assetId);
            
            if (!exists)
            {
                context.DownloadedAssets.Add(new DownloadedAsset
                {
                    AlbumId = albumId,
                    AssetId = assetId,
                    DownloadedAlbumId = 0 // Will be updated when album is saved
                });
                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording downloaded asset {AssetId}", assetId);
        }
    }

    private async Task SaveDownloadedAlbumAsync(string albumId, string albumName, int photoCount, string zipFilePath)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Remove existing album to avoid duplicates
            var existingAlbum = await context.DownloadedAlbums
                .FirstOrDefaultAsync(a => a.AlbumId == albumId);
            
            if (existingAlbum != null)
            {
                context.DownloadedAlbums.Remove(existingAlbum);
                await context.SaveChangesAsync();
            }

            var fileInfo = new FileInfo(zipFilePath);
            var downloadedAlbum = new DownloadedAlbum
            {
                AlbumId = albumId,
                AlbumName = albumName,
                PhotoCount = photoCount,
                TotalSize = fileInfo.Length,
                CreatedAt = DateTime.UtcNow,
                FilePath = zipFilePath // Store file path instead of data
            };

            context.DownloadedAlbums.Add(downloadedAlbum);
            await context.SaveChangesAsync();

            // Update asset records with album ID
            var assets = await context.DownloadedAssets
                .Where(da => da.AlbumId == albumId)
                .ToListAsync();
            
            foreach (var asset in assets)
            {
                asset.DownloadedAlbumId = downloadedAlbum.Id;
            }
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving downloaded album metadata");
        }
    }

    private async Task UpdateTaskAsync(string taskId, Models.TaskStatus status, string? message = null, 
        int? progress = null, int? total = null)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var task = await context.BackgroundTasks.FindAsync(taskId);
            if (task != null)
            {
                task.Status = status;
                if (message != null) task.CurrentStep = message;
                if (progress.HasValue) task.Progress = progress.Value;
                if (total.HasValue) task.Total = total.Value;
                if (status == Models.TaskStatus.Completed) task.CompletedAt = DateTime.UtcNow;

                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update task {TaskId}", taskId);
        }
    }

    private Task CleanupDownloadAsync(string taskId)
    {
        try
        {
            var zipFilePath = Path.Combine(_downloadsPath, $"{taskId}.zip");
            if (File.Exists(zipFilePath))
            {
                File.Delete(zipFilePath);
                _logger.LogInformation("Cleaned up ZIP file for task {TaskId}", taskId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up download {TaskId}", taskId);
        }
        
        return Task.CompletedTask;
    }

    private async Task<(string?, string?)> GetImmichSettingsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var urlSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "Immich:Url");
            var apiKeySetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "Immich:ApiKey");
            
            return (urlSetting?.Value, apiKeySetting?.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Immich settings from database");
            return (null, null);
        }
    }

    /// <summary>
    /// Notifies connected clients about task progress via SignalR.
    /// </summary>
    private async Task NotifyProgressAsync(string taskId, Models.TaskStatus status, string? message = null, int? progress = null, int? total = null)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("TaskStatusUpdated", new
            {
                taskId,
                status = status.ToString().ToLowerInvariant(),
                message,
                progress,
                total
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending progress notification for task {TaskId}", taskId);
        }
    }
}