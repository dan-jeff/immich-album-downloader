using System.IO.Compression;
using ImmichDownloader.Web.Models;
using ImmichDownloader.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace ImmichDownloader.Web.Services;

/// <summary>
/// Service interface for streaming image resizing that writes processed images directly to disk.
/// </summary>
public interface IStreamingResizeService
{
    /// <summary>
    /// Starts resizing images from a downloaded album using streaming processing.
    /// </summary>
    /// <param name="taskId">The unique identifier for the resize task.</param>
    /// <param name="downloadedAlbumId">The ID of the downloaded album to resize.</param>
    /// <param name="profileId">The ID of the resize profile to apply.</param>
    /// <param name="cancellationToken">Token to cancel the resize operation.</param>
    /// <returns>A task representing the asynchronous resize operation.</returns>
    Task StartResizeAsync(string taskId, int downloadedAlbumId, int profileId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a stream for reading a completed resize operation.
    /// </summary>
    /// <param name="taskId">The ID of the completed resize task.</param>
    /// <returns>A stream for reading the resized images, or null if not found.</returns>
    Stream? GetResizeStream(string taskId);
}

/// <summary>
/// Service that handles image resizing by streaming processed images directly to disk as ZIP files.
/// This approach reduces memory usage compared to in-memory processing.
/// </summary>
public class StreamingResizeService : IStreamingResizeService
{
    private readonly ILogger<StreamingResizeService> _logger;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITaskProgressService _progressService;
    private readonly string _resizedPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamingResizeService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for logging operations and errors.</param>
    /// <param name="imageProcessingService">Service for processing and resizing images.</param>
    /// <param name="scopeFactory">Factory for creating service scopes for database operations.</param>
    /// <param name="progressService">Service for tracking and notifying about task progress.</param>
    /// <param name="configuration">Configuration provider for accessing application settings.</param>
    public StreamingResizeService(
        ILogger<StreamingResizeService> logger,
        IImageProcessingService imageProcessingService,
        IServiceScopeFactory scopeFactory,
        ITaskProgressService progressService,
        IConfiguration configuration)
    {
        _logger = logger;
        _imageProcessingService = imageProcessingService;
        _scopeFactory = scopeFactory;
        _progressService = progressService;
        _resizedPath = Path.Combine(configuration.GetValue<string>("DataPath") ?? "data", "resized");
        
        // Ensure resized directory exists
        Directory.CreateDirectory(_resizedPath);
    }

    /// <summary>
    /// Starts resizing images from a downloaded album using streaming processing,
    /// writing processed images directly to a ZIP file to minimize memory usage.
    /// </summary>
    /// <param name="taskId">The unique identifier for the resize task.</param>
    /// <param name="downloadedAlbumId">The ID of the downloaded album to resize.</param>
    /// <param name="profileId">The ID of the resize profile to apply.</param>
    /// <param name="cancellationToken">Token to cancel the resize operation.</param>
    /// <returns>A task representing the asynchronous resize operation.</returns>
    public async Task StartResizeAsync(string taskId, int downloadedAlbumId, int profileId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting streaming resize for album {AlbumId} with profile {ProfileId}, task {TaskId}", 
                downloadedAlbumId, profileId, taskId);

            await _progressService.NotifyProgressAsync(taskId, TaskType.Resize, Models.TaskStatus.InProgress);

            // Get album and profile data
            var (album, profile) = await GetAlbumAndProfileAsync(downloadedAlbumId, profileId);
            if (album == null || profile == null)
            {
                await UpdateTaskAsync(taskId, Models.TaskStatus.Error, "Album or profile not found");
                return;
            }

            // Get source ZIP stream
            Stream? sourceStream = null;
            try
            {
                if (!string.IsNullOrEmpty(album.FilePath) && File.Exists(album.FilePath))
                {
                    sourceStream = new FileStream(album.FilePath, FileMode.Open, FileAccess.Read);
                }
                else if (album.Chunks.Any())
                {
                    // Fallback to legacy chunk-based data
                    var allData = album.Chunks.OrderBy(c => c.ChunkIndex).SelectMany(c => c.ChunkData).ToArray();
                    sourceStream = new MemoryStream(allData);
                }
                else
                {
                    await UpdateTaskAsync(taskId, Models.TaskStatus.Error, "No download data found");
                    return;
                }

                // Count total files in source ZIP
                int totalFiles = 0;
                using (var tempArchive = new ZipArchive(sourceStream, ZipArchiveMode.Read, true))
                {
                    totalFiles = tempArchive.Entries.Count;
                }
                sourceStream.Position = 0; // Reset stream position

                if (totalFiles == 0)
                {
                    await UpdateTaskAsync(taskId, Models.TaskStatus.Completed, "No images to process");
                    return;
                }

                await UpdateTaskAsync(taskId, Models.TaskStatus.InProgress, "Processing images...", 0, totalFiles);

                // Create output ZIP file for streaming
                var outputZipPath = Path.Combine(_resizedPath, $"{taskId}.zip");
                var processedCount = 0;

                using (var sourceArchive = new ZipArchive(sourceStream, ZipArchiveMode.Read))
                using (var outputStream = new FileStream(outputZipPath, FileMode.Create, FileAccess.Write))
                using (var outputArchive = new ZipArchive(outputStream, ZipArchiveMode.Create))
                {
                    foreach (var entry in sourceArchive.Entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (entry.Length == 0) continue; // Skip empty entries

                        try
                        {
                            // Read image data from source ZIP
                            using var entryStream = entry.Open();
                            using var memoryStream = new MemoryStream();
                            await entryStream.CopyToAsync(memoryStream, cancellationToken);
                            var imageData = memoryStream.ToArray();

                            // Process image
                            var (success, processedImage, error) = await _imageProcessingService.ResizeImageAsync(
                                imageData, entry.Name, profile);

                            if (success && processedImage != null)
                            {
                                // Write processed image directly to output ZIP
                                var outputEntry = outputArchive.CreateEntry(entry.Name);
                                using var outputEntryStream = outputEntry.Open();
                                await outputEntryStream.WriteAsync(processedImage, cancellationToken);
                                
                                processedCount++;
                            }
                            else if (!string.IsNullOrEmpty(error))
                            {
                                _logger.LogWarning("Skipping image {FileName}: {Error}", entry.Name, error);
                            }

                            // Update progress every 10 images
                            if (processedCount % 10 == 0)
                            {
                                await UpdateTaskAsync(taskId, Models.TaskStatus.InProgress, 
                                    $"Processed {processedCount}/{totalFiles} images", processedCount, totalFiles);
                                await _progressService.NotifyProgressAsync(taskId, TaskType.Resize, 
                                    Models.TaskStatus.InProgress, processedCount, totalFiles);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing image {FileName}", entry.Name);
                            continue; // Skip this image and continue with others
                        }
                    }
                }

                // Save task completion with file path
                var fileInfo = new FileInfo(outputZipPath);
                await UpdateTaskAsync(taskId, Models.TaskStatus.Completed, "Resize complete!", 
                    processedCount, totalFiles, outputZipPath, fileInfo.Length, processedCount);

                await _progressService.NotifyTaskCompletedAsync(taskId, TaskType.Resize);

                _logger.LogInformation("Completed streaming resize for task {TaskId}, processed {Count} images", 
                    taskId, processedCount);
            }
            finally
            {
                sourceStream?.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            await UpdateTaskAsync(taskId, Models.TaskStatus.Error, "Resize cancelled");
            await CleanupResizeAsync(taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in streaming resize task {TaskId}", taskId);
            await UpdateTaskAsync(taskId, Models.TaskStatus.Error, $"Error: {ex.Message}");
            await CleanupResizeAsync(taskId);
        }
    }

    /// <summary>
    /// Gets a stream for reading a completed resize operation ZIP file.
    /// Returns null if the resize file is not found or cannot be opened.
    /// </summary>
    /// <param name="taskId">The ID of the completed resize task.</param>
    /// <returns>A stream for reading the resized images, or null if not found.</returns>
    public Stream? GetResizeStream(string taskId)
    {
        var zipFilePath = Path.Combine(_resizedPath, $"{taskId}.zip");
        
        if (!File.Exists(zipFilePath))
        {
            _logger.LogWarning("Resized ZIP file not found for task {TaskId}", taskId);
            return null;
        }

        try
        {
            return new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening resize stream for task {TaskId}", taskId);
            return null;
        }
    }

    private async Task<(DownloadedAlbum?, ResizeProfile?)> GetAlbumAndProfileAsync(int downloadedAlbumId, int profileId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var album = await context.DownloadedAlbums
            .Include(a => a.Chunks)
            .FirstOrDefaultAsync(a => a.Id == downloadedAlbumId);

        var profile = await context.ResizeProfiles
            .FirstOrDefaultAsync(p => p.Id == profileId);

        return (album, profile);
    }

    private async Task UpdateTaskAsync(string taskId, Models.TaskStatus status, string? message = null, 
        int? progress = null, int? total = null, string? filePath = null, long? fileSize = null, int? processedCount = null)
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
                
                // Store file path instead of ZIP data for streaming
                if (filePath != null) task.FilePath = filePath;
                if (fileSize.HasValue) task.FileSize = fileSize.Value;

                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update task {TaskId}", taskId);
        }
    }

    private Task CleanupResizeAsync(string taskId)
    {
        try
        {
            var zipFilePath = Path.Combine(_resizedPath, $"{taskId}.zip");
            if (File.Exists(zipFilePath))
            {
                File.Delete(zipFilePath);
                _logger.LogInformation("Cleaned up resized ZIP file for task {TaskId}", taskId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up resize {TaskId}", taskId);
        }
        
        return Task.CompletedTask;
    }
}