using Microsoft.EntityFrameworkCore;
using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using System.IO.Compression;

namespace ImmichDownloader.Web.Services;

/// <summary>
/// Service responsible for resizing album images according to configured profiles.
/// Handles the complete resize workflow including image extraction, processing, and progress tracking.
/// </summary>
public class ResizeService : IResizeService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly ITaskProgressService _progressService;
    private readonly ILogger<ResizeService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResizeService"/> class.
    /// </summary>
    /// <param name="scopeFactory">Factory for creating service scopes for database operations.</param>
    /// <param name="imageProcessingService">Service for processing and resizing images.</param>
    /// <param name="progressService">Service for tracking and notifying about task progress.</param>
    /// <param name="logger">Logger instance for logging operations and errors.</param>
    public ResizeService(
        IServiceScopeFactory scopeFactory,
        IImageProcessingService imageProcessingService,
        ITaskProgressService progressService,
        ILogger<ResizeService> logger)
    {
        _scopeFactory = scopeFactory;
        _imageProcessingService = imageProcessingService;
        _progressService = progressService;
        _logger = logger;
    }

    /// <summary>
    /// Resizes all images in a downloaded album according to the specified resize profile.
    /// Extracts images from stored chunks, processes them using the image processing service,
    /// and tracks progress throughout the operation.
    /// </summary>
    /// <param name="taskId">The unique identifier for the background task.</param>
    /// <param name="downloadedAlbumId">The ID of the downloaded album containing the images to resize.</param>
    /// <param name="profileId">The ID of the resize profile to apply to the images.</param>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    /// <returns>A task representing the asynchronous resize operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task ResizeAlbumAsync(string taskId, int downloadedAlbumId, int profileId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Get profile
            var profile = await context.ResizeProfiles.FindAsync(profileId);
            if (profile == null)
            {
                await UpdateTaskAsync(taskId, Models.TaskStatus.Error, "Profile not found");
                return;
            }

            // Get downloaded album
            var downloadedAlbum = await context.DownloadedAlbums
                .Include(d => d.Chunks)
                .FirstOrDefaultAsync(d => d.Id == downloadedAlbumId);

            if (downloadedAlbum == null)
            {
                await UpdateTaskAsync(taskId, Models.TaskStatus.Error, "Album not found");
                return;
            }

            await UpdateTaskAsync(taskId, Models.TaskStatus.InProgress, "Processing images...");
            await _progressService.NotifyProgressAsync(taskId, TaskType.Resize, Models.TaskStatus.InProgress);

            // Extract images from chunks
            var allImages = new List<(string FileName, byte[] Data)>();

            foreach (var chunk in downloadedAlbum.Chunks.OrderBy(c => c.ChunkIndex))
            {
                using var chunkStream = new MemoryStream(chunk.ChunkData);
                using var archive = new ZipArchive(chunkStream, ZipArchiveMode.Read);

                foreach (var entry in archive.Entries)
                {
                    using var entryStream = entry.Open();
                    using var memoryStream = new MemoryStream();
                    await entryStream.CopyToAsync(memoryStream, cancellationToken);
                    allImages.Add((entry.Name, memoryStream.ToArray()));
                }
            }

            var totalFiles = allImages.Count;
            await UpdateTaskAsync(taskId, Models.TaskStatus.InProgress, null, 0, totalFiles);

            // Process images with progress reporting
            var progress = new Progress<int>(async processedCount =>
            {
                await UpdateTaskAsync(taskId, Models.TaskStatus.InProgress, 
                    $"Processing images... ({processedCount}/{totalFiles})", 
                    processedCount, totalFiles);
                
                await _progressService.NotifyProgressAsync(taskId, TaskType.Resize, 
                    Models.TaskStatus.InProgress, processedCount, totalFiles);
            });

            var (success, zipData, error) = await _imageProcessingService.ProcessImagesAsync(
                allImages, profile, progress, cancellationToken);

            if (!success || zipData == null)
            {
                await UpdateTaskAsync(taskId, Models.TaskStatus.Error, $"Error: {error}");
                await _progressService.NotifyTaskErrorAsync(taskId, TaskType.Resize, error ?? "Unknown error");
                return;
            }

            // Save result
            await UpdateTaskAsync(taskId, Models.TaskStatus.Completed, "Resize complete!", 
                totalFiles, totalFiles, zipData, zipData.Length, allImages.Count);
            
            await _progressService.NotifyTaskCompletedAsync(taskId, TaskType.Resize);
        }
        catch (OperationCanceledException)
        {
            await UpdateTaskAsync(taskId, Models.TaskStatus.Error, "Resize cancelled");
            await _progressService.NotifyTaskErrorAsync(taskId, TaskType.Resize, "Resize cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in resize task {TaskId}", taskId);
            await UpdateTaskAsync(taskId, Models.TaskStatus.Error, $"Error: {ex.Message}");
            await _progressService.NotifyTaskErrorAsync(taskId, TaskType.Resize, ex.Message);
        }
    }

    /// <summary>
    /// Updates the status and progress information for a background task in the database.
    /// </summary>
    /// <param name="taskId">The unique identifier for the background task to update.</param>
    /// <param name="status">The new status of the task.</param>
    /// <param name="currentStep">Optional description of the current step being executed.</param>
    /// <param name="progress">Optional current progress value.</param>
    /// <param name="total">Optional total number of items to process.</param>
    /// <param name="zipData">Optional binary data of the completed ZIP file.</param>
    /// <param name="zipSize">Optional size of the ZIP file in bytes.</param>
    /// <param name="processedCount">Optional count of items that have been processed.</param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    private async Task UpdateTaskAsync(string taskId, Models.TaskStatus status, string? currentStep = null, 
        int? progress = null, int? total = null, byte[]? zipData = null, long? zipSize = null, int? processedCount = null)
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
            if (zipData != null) task.ZipData = zipData;
            if (zipSize.HasValue) task.ZipSize = zipSize.Value;
            if (processedCount.HasValue) task.ProcessedCount = processedCount.Value;
            if (status == Models.TaskStatus.Completed) task.CompletedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();
        }
    }
}