using Microsoft.EntityFrameworkCore;
using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using Immich.Data.Models;
using System.IO.Compression;

namespace ImmichDownloader.Web.Services;

/// <summary>
/// Implementation of the download service for downloading albums from the Immich server.
/// This service handles the complete download process including chunking, progress tracking,
/// and saving downloaded assets to the database.
/// </summary>
public class DownloadService : IDownloadService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IImmichService _immichService;
    private readonly ITaskProgressService _progressService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DownloadService> _logger;
    
    /// <summary>
    /// The number of photos to process in each chunk during download operations.
    /// This helps manage memory usage and provides better progress tracking.
    /// </summary>
    private const int CHUNK_SIZE = 50; // Photos per chunk

    /// <summary>
    /// Initializes a new instance of the DownloadService class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="immichService">The Immich service for communicating with the Immich server.</param>
    /// <param name="progressService">The progress service for tracking and reporting download progress.</param>
    /// <param name="configuration">The configuration provider for application settings.</param>
    /// <param name="logger">The logger instance for this service.</param>
    public DownloadService(
        IServiceScopeFactory scopeFactory,
        IImmichService immichService,
        ITaskProgressService progressService,
        IConfiguration configuration,
        ILogger<DownloadService> logger)
    {
        _scopeFactory = scopeFactory;
        _immichService = immichService;
        _progressService = progressService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Downloads an album from the Immich server asynchronously.
    /// The download process includes fetching album information, downloading all assets in chunks,
    /// creating ZIP archives, and saving everything to the database with progress tracking.
    /// </summary>
    /// <param name="taskId">The unique identifier of the background task associated with this download.</param>
    /// <param name="albumId">The unique identifier of the album to download from the Immich server.</param>
    /// <param name="albumName">The name of the album being downloaded (used for logging and progress reporting).</param>
    /// <param name="cancellationToken">A cancellation token to cancel the download operation.</param>
    /// <returns>A task that represents the asynchronous download operation.</returns>
    /// <exception cref="ArgumentException">Thrown when taskId, albumId, or albumName is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the service dependencies are not properly configured.</exception>
    /// <exception cref="HttpRequestException">Thrown when communication with the Immich server fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    public async Task DownloadAlbumAsync(string taskId, string albumId, string albumName, CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== Starting download for task {TaskId}, album {AlbumId} ({AlbumName}) ===", taskId, albumId, albumName);
        try
        {
            // Configure Immich service from database settings
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var immichUrl = await GetSettingAsync(context, "Immich:Url");
            var apiKey = await GetSettingAsync(context, "Immich:ApiKey");
            
            _logger.LogInformation("Download service - Immich URL: '{Url}', API Key set: {ApiKeySet}", immichUrl, !string.IsNullOrEmpty(apiKey));
            
            if (string.IsNullOrEmpty(immichUrl) || string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("Download service - Configuration missing: URL={Url}, APIKey={ApiKey}", immichUrl ?? "null", apiKey ?? "null");
                await UpdateTaskAsync(taskId, Models.TaskStatus.Error, "Immich configuration not found");
                await _progressService.NotifyTaskErrorAsync(taskId, TaskType.Download, "Immich configuration not found");
                return;
            }
            
            _logger.LogInformation("Download service - Configuring Immich service with URL: {Url}", immichUrl);
            _immichService.Configure(immichUrl, apiKey);

            // Update task status
            _logger.LogInformation("Download service - Updating task status to InProgress");
            await UpdateTaskAsync(taskId, Models.TaskStatus.InProgress, "Fetching album photos...");
            await _progressService.NotifyProgressAsync(taskId, TaskType.Download, Models.TaskStatus.InProgress);

            // Get album info
            _logger.LogInformation("Download service - Fetching album info for album {AlbumId}", albumId);
            var (success, albumInfo, error) = await _immichService.GetAlbumInfoAsync(albumId);
            _logger.LogInformation("Download service - GetAlbumInfoAsync result: success={Success}, albumInfo null={AlbumInfoNull}, error={Error}", 
                success, albumInfo == null, error ?? "none");
            
            if (!success || albumInfo == null)
            {
                _logger.LogError("Download service - Failed to get album info: {Error}", error ?? "Unknown error");
                await UpdateTaskAsync(taskId, Models.TaskStatus.Error, $"Error: {error}");
                await _progressService.NotifyTaskErrorAsync(taskId, TaskType.Download, error ?? "Unknown error");
                return;
            }

            var allPhotos = albumInfo.Assets.Where(a => a.Type == "IMAGE").ToList();
            
            // Check which assets are already downloaded
            var existingAssetIds = await context.DownloadedAssets
                .Where(da => da.AlbumId == albumId)
                .Select(da => da.AssetId)
                .ToListAsync();
            
            // Filter to only include photos that haven't been downloaded yet
            var photos = allPhotos.Where(a => !existingAssetIds.Contains(a.Id)).ToList();
            var totalPhotos = allPhotos.Count;
            var alreadyDownloaded = existingAssetIds.Count;
            var toDownload = photos.Count;
            
            _logger.LogInformation("Download service - Album {AlbumId} has {TotalPhotos} total photos, {AlreadyDownloaded} already downloaded, {ToDownload} to download", 
                albumId, totalPhotos, alreadyDownloaded, toDownload);

            if (toDownload == 0)
            {
                _logger.LogInformation("Download service - All photos already downloaded for album {AlbumId}", albumId);
                await UpdateTaskAsync(taskId, Models.TaskStatus.Completed, "All photos already downloaded");
                await _progressService.NotifyTaskCompletedAsync(taskId, TaskType.Download);
                return;
            }

            // Update task with total count
            _logger.LogInformation("Download service - Updating task with photo count: {ToDownload} to download", toDownload);
            await UpdateTaskAsync(taskId, Models.TaskStatus.InProgress, "Downloading new photos...", 0, toDownload);

            // Process in chunks
            var chunks = new List<byte[]>();
            var downloadedCount = 0;

            for (int chunkStart = 0; chunkStart < toDownload; chunkStart += CHUNK_SIZE)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var chunkEnd = Math.Min(chunkStart + CHUNK_SIZE, toDownload);
                var chunkPhotos = photos.Skip(chunkStart).Take(CHUNK_SIZE).ToList();

                // Download photos for this chunk in parallel
                var chunkImages = new List<(string FileName, byte[] Data)>();
                var semaphore = new SemaphoreSlim(5, 5); // Limit to 5 concurrent downloads

                var downloadTasks = chunkPhotos.Select(async photo =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        var (downloadSuccess, data, downloadError) = await _immichService.DownloadAssetAsync(photo.Id);
                        if (downloadSuccess && data != null)
                        {
                            lock (chunkImages)
                            {
                                chunkImages.Add((photo.OriginalFileName, data));
                            }
                            Interlocked.Increment(ref downloadedCount);
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

                // Update progress
                await UpdateTaskAsync(taskId, Models.TaskStatus.InProgress, $"Downloaded chunk {chunks.Count + 1}", chunkEnd, toDownload);
                await _progressService.NotifyProgressAsync(taskId, TaskType.Download, Models.TaskStatus.InProgress, chunkEnd, toDownload);

                // Create ZIP for this chunk
                if (chunkImages.Any())
                {
                    using var chunkZip = new MemoryStream();
                    using (var archive = new ZipArchive(chunkZip, ZipArchiveMode.Create, true))
                    {
                        foreach (var (fileName, data) in chunkImages)
                        {
                            var entry = archive.CreateEntry(fileName);
                            using var entryStream = entry.Open();
                            await entryStream.WriteAsync(data, cancellationToken);
                        }
                    }
                    chunks.Add(chunkZip.ToArray());
                }

                // Free memory
                chunkImages.Clear();
            }

            // Save to database
            await SaveDownloadedAlbumAsync(albumId, albumName, downloadedCount, chunks, photos.Take(downloadedCount).ToList());

            // Update task as completed
            await UpdateTaskAsync(taskId, Models.TaskStatus.Completed, "Download complete!");
            await _progressService.NotifyTaskCompletedAsync(taskId, TaskType.Download);
        }
        catch (OperationCanceledException)
        {
            await UpdateTaskAsync(taskId, Models.TaskStatus.Error, "Download cancelled");
            await _progressService.NotifyTaskErrorAsync(taskId, TaskType.Download, "Download cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in download task {TaskId}", taskId);
            await UpdateTaskAsync(taskId, Models.TaskStatus.Error, $"Error: {ex.Message}");
            await _progressService.NotifyTaskErrorAsync(taskId, TaskType.Download, ex.Message);
        }
    }

    /// <summary>
    /// Updates a background task's status and progress information in the database.
    /// This method is used to track the progress of download operations and notify clients.
    /// </summary>
    /// <param name="taskId">The unique identifier of the task to update.</param>
    /// <param name="status">The new status to set for the task.</param>
    /// <param name="currentStep">Optional description of the current step being performed.</param>
    /// <param name="progress">Optional current progress value.</param>
    /// <param name="total">Optional total value for progress calculations.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    private async Task UpdateTaskAsync(string taskId, Models.TaskStatus status, string? currentStep = null, int? progress = null, int? total = null)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var task = await context.BackgroundTasks.FindAsync(taskId);
            if (task != null)
            {
                task.Status = status;
                if (currentStep != null) task.CurrentStep = currentStep;
                if (progress.HasValue) task.Progress = progress.Value;
                if (total.HasValue) task.Total = total.Value;
                if (status == Models.TaskStatus.Completed) task.CompletedAt = DateTime.UtcNow;

                await context.SaveChangesAsync();
            }
            else
            {
                _logger.LogWarning("Task {TaskId} not found when trying to update", taskId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update task {TaskId} to status {Status}: {Error}. Inner exception: {InnerException}", 
                taskId, status, ex.Message, ex.InnerException?.Message);
            // Don't re-throw to prevent breaking the download process
        }
    }

    /// <summary>
    /// Saves a downloaded album and its associated data to the database.
    /// This includes the album metadata, ZIP chunks, and individual asset records for incremental downloads.
    /// </summary>
    /// <param name="albumId">The unique identifier of the album.</param>
    /// <param name="albumName">The name of the album.</param>
    /// <param name="photoCount">The number of photos downloaded.</param>
    /// <param name="chunks">List of ZIP archive chunks containing the downloaded photos.</param>
    /// <param name="downloadedAssets">List of individual asset models that were downloaded.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    /// <exception cref="Exception">Thrown when the save operation fails.</exception>
    private async Task SaveDownloadedAlbumAsync(string albumId, string albumName, int photoCount, List<byte[]> chunks, List<AlbumInfoAssetModel> downloadedAssets)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Check if album already exists and remove it to avoid duplicates
            var existingAlbum = await context.DownloadedAlbums
                .Include(a => a.Chunks)
                .FirstOrDefaultAsync(a => a.AlbumId == albumId);
            
            if (existingAlbum != null)
            {
                _logger.LogInformation("Removing existing downloaded album {AlbumId} to avoid duplicates", albumId);
                context.DownloadedAlbums.Remove(existingAlbum);
                await context.SaveChangesAsync();
            }

            var downloadedAlbum = new DownloadedAlbum
            {
                AlbumId = albumId,
                AlbumName = albumName,
                PhotoCount = photoCount,
                TotalSize = chunks.Sum(c => (long)c.Length),
                ChunkCount = chunks.Count
            };

            // Save the album first to get its ID
            context.DownloadedAlbums.Add(downloadedAlbum);
            await context.SaveChangesAsync();
            
            // Create chunks and add them to the album's navigation property
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = new AlbumChunk
                {
                    AlbumId = albumId,
                    DownloadedAlbumId = downloadedAlbum.Id,
                    ChunkIndex = i,
                    ChunkData = chunks[i],
                    ChunkSize = chunks[i].Length,
                    PhotoCount = chunks.Count > 0 ? photoCount / chunks.Count + (i < photoCount % chunks.Count ? 1 : 0) : 0,
                    DownloadedAlbum = downloadedAlbum
                };

                downloadedAlbum.Chunks.Add(chunk);
                _logger.LogInformation("Created chunk {ChunkIndex} for album {AlbumName}", i, albumName);
            }

            // Save individual asset records for incremental downloads
            foreach (var asset in downloadedAssets)
            {
                var downloadedAsset = new DownloadedAsset
                {
                    AssetId = asset.Id,
                    AlbumId = albumId,
                    FileName = asset.OriginalFileName,
                    FileSize = 0, // We don't have individual file sizes
                    DownloadedAlbumId = downloadedAlbum.Id,
                    DownloadedAt = DateTime.UtcNow
                };
                
                context.DownloadedAssets.Add(downloadedAsset);
                _logger.LogInformation("Added asset record for {AssetId} in album {AlbumName}", asset.Id, albumName);
            }

            // Save the chunks and assets
            _logger.LogInformation("Saving album {AlbumName} with {ChunkCount} chunks and {AssetCount} asset records", 
                albumName, chunks.Count, downloadedAssets.Count);
            await context.SaveChangesAsync();
            _logger.LogInformation("Successfully saved downloaded album {AlbumName} with {ChunkCount} chunks and {AssetCount} asset records", 
                albumName, chunks.Count, downloadedAssets.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save downloaded album {AlbumId}: {Error}. Inner exception: {InnerException}", 
                albumId, ex.Message, ex.InnerException?.Message);
            throw; // Re-throw to let the caller handle it
        }
    }

    /// <summary>
    /// Retrieves a setting value from the application settings in the database.
    /// </summary>
    /// <param name="context">The database context to use for the query.</param>
    /// <param name="key">The key of the setting to retrieve.</param>
    /// <returns>The setting value if found, otherwise null.</returns>
    private async Task<string?> GetSettingAsync(ApplicationDbContext context, string key)
    {
        var setting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value;
    }
}