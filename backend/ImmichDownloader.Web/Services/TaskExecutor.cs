using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using ImmichDownloader.Web.Hubs;

namespace ImmichDownloader.Web.Services;

/// <summary>
/// Simplified task execution service that replaces the complex background task queue system.
/// Handles task queuing, execution, and progress tracking in a unified service.
/// </summary>
public class TaskExecutor : BackgroundService
{
    private readonly Channel<TaskRequest> _taskQueue;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<ProgressHub> _hubContext;
    private readonly ILogger<TaskExecutor> _logger;

    public TaskExecutor(IServiceProvider serviceProvider, IHubContext<ProgressHub> hubContext, ILogger<TaskExecutor> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
        
        // Create bounded channel for task queue (limit concurrent tasks)
        var options = new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };
        _taskQueue = Channel.CreateBounded<TaskRequest>(options);
    }

    /// <summary>
    /// Queues a download task for execution.
    /// </summary>
    /// <param name="taskId">Unique identifier for the task.</param>
    /// <param name="albumId">Immich album ID to download.</param>
    /// <param name="albumName">Name of the album for display purposes.</param>
    /// <returns>True if the task was queued successfully, false if the queue is full.</returns>
    public async Task<bool> QueueDownloadAsync(string taskId, string albumId, string albumName)
    {
        var request = new DownloadTaskRequest(taskId, albumId, albumName);
        
        try
        {
            await _taskQueue.Writer.WriteAsync(request);
            _logger.LogInformation("Download task {TaskId} queued for album {AlbumName}", taskId, albumName);
            return true;
        }
        catch (InvalidOperationException)
        {
            _logger.LogError("Failed to queue download task {TaskId} - queue is closed", taskId);
            return false;
        }
    }

    /// <summary>
    /// Queues a resize task for execution.
    /// </summary>
    /// <param name="taskId">Unique identifier for the task.</param>
    /// <param name="downloadedAlbumId">ID of the downloaded album to resize.</param>
    /// <param name="profileId">Resize profile ID to use.</param>
    /// <returns>True if the task was queued successfully, false if the queue is full.</returns>
    public async Task<bool> QueueResizeAsync(string taskId, int downloadedAlbumId, int profileId)
    {
        var request = new ResizeTaskRequest(taskId, downloadedAlbumId, profileId);
        
        try
        {
            await _taskQueue.Writer.WriteAsync(request);
            _logger.LogInformation("Resize task {TaskId} queued for album {AlbumId} with profile {ProfileId}", 
                taskId, downloadedAlbumId, profileId);
            return true;
        }
        catch (InvalidOperationException)
        {
            _logger.LogError("Failed to queue resize task {TaskId} - queue is closed", taskId);
            return false;
        }
    }

    /// <summary>
    /// Background service execution loop that processes queued tasks.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TaskExecutor background service started");

        try
        {
            await foreach (var taskRequest in _taskQueue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ExecuteTaskAsync(taskRequest, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing task {TaskId}", taskRequest.TaskId);
                    await UpdateTaskStatusAsync(taskRequest.TaskId, Models.TaskStatus.Error, ex.Message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("TaskExecutor background service is stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TaskExecutor background service encountered an error");
        }
    }

    /// <summary>
    /// Executes a specific task based on its type.
    /// </summary>
    private async Task ExecuteTaskAsync(TaskRequest request, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        _logger.LogInformation("Executing task {TaskId} of type {TaskType}", request.TaskId, request.GetType().Name);

        switch (request)
        {
            case DownloadTaskRequest downloadRequest:
                await ExecuteDownloadAsync(downloadRequest, scope, cancellationToken);
                break;

            case ResizeTaskRequest resizeRequest:
                await ExecuteResizeAsync(resizeRequest, scope, cancellationToken);
                break;

            default:
                _logger.LogError("Unknown task request type: {TaskType}", request.GetType().Name);
                await UpdateTaskStatusAsync(request.TaskId, Models.TaskStatus.Error, "Unknown task type");
                break;
        }
    }

    /// <summary>
    /// Executes a download task using the streaming download service.
    /// </summary>
    private async Task ExecuteDownloadAsync(DownloadTaskRequest request, IServiceScope scope, CancellationToken cancellationToken)
    {
        try
        {
            await UpdateTaskStatusAsync(request.TaskId, Models.TaskStatus.InProgress, "Starting download...");

            var downloadService = scope.ServiceProvider.GetRequiredService<IStreamingDownloadService>();
            await downloadService.StartDownloadAsync(request.TaskId, request.AlbumId, request.AlbumName, cancellationToken);

            await UpdateTaskStatusAsync(request.TaskId, Models.TaskStatus.Completed, "Download completed");
            _logger.LogInformation("Download task {TaskId} completed successfully", request.TaskId);
        }
        catch (OperationCanceledException)
        {
            await UpdateTaskStatusAsync(request.TaskId, Models.TaskStatus.Error, "Download cancelled");
            _logger.LogWarning("Download task {TaskId} was cancelled", request.TaskId);
        }
        catch (Exception ex)
        {
            await UpdateTaskStatusAsync(request.TaskId, Models.TaskStatus.Error, $"Download failed: {ex.Message}");
            _logger.LogError(ex, "Download task {TaskId} failed", request.TaskId);
        }
    }

    /// <summary>
    /// Executes a resize task using the streaming resize service.
    /// </summary>
    private async Task ExecuteResizeAsync(ResizeTaskRequest request, IServiceScope scope, CancellationToken cancellationToken)
    {
        try
        {
            await UpdateTaskStatusAsync(request.TaskId, Models.TaskStatus.InProgress, "Starting resize...");

            var resizeService = scope.ServiceProvider.GetRequiredService<IStreamingResizeService>();
            await resizeService.StartResizeAsync(request.TaskId, request.DownloadedAlbumId, request.ProfileId, cancellationToken);

            await UpdateTaskStatusAsync(request.TaskId, Models.TaskStatus.Completed, "Resize completed");
            _logger.LogInformation("Resize task {TaskId} completed successfully", request.TaskId);
        }
        catch (OperationCanceledException)
        {
            await UpdateTaskStatusAsync(request.TaskId, Models.TaskStatus.Error, "Resize cancelled");
            _logger.LogWarning("Resize task {TaskId} was cancelled", request.TaskId);
        }
        catch (Exception ex)
        {
            await UpdateTaskStatusAsync(request.TaskId, Models.TaskStatus.Error, $"Resize failed: {ex.Message}");
            _logger.LogError(ex, "Resize task {TaskId} failed", request.TaskId);
        }
    }

    /// <summary>
    /// Updates task status in the database and notifies connected clients via SignalR.
    /// </summary>
    private async Task UpdateTaskStatusAsync(string taskId, Models.TaskStatus status, string? message = null)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var task = await context.BackgroundTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task != null)
            {
                task.Status = status;
                if (message != null)
                {
                    task.CurrentStep = message;
                }
                
                if (status == Models.TaskStatus.Completed || status == Models.TaskStatus.Error)
                {
                    task.CompletedAt = DateTime.UtcNow;
                }

                await context.SaveChangesAsync();

                // Notify connected clients via SignalR
                await _hubContext.Clients.All.SendAsync("TaskStatusUpdated", new
                {
                    taskId = task.Id,
                    status = status.ToString().ToLowerInvariant(),
                    message = task.CurrentStep,
                    progress = task.Progress,
                    total = task.Total
                });

                _logger.LogDebug("Updated task {TaskId} status to {Status}: {Message}", taskId, status, message);
            }
            else
            {
                _logger.LogWarning("Task {TaskId} not found in database", taskId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task {TaskId} status", taskId);
        }
    }

    /// <summary>
    /// Gracefully shuts down the task executor.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TaskExecutor is stopping - completing queued tasks");
        
        // Stop accepting new tasks - handle case where channel is already closed
        try
        {
            _taskQueue.Writer.Complete();
        }
        catch (InvalidOperationException)
        {
            // Channel is already closed, which is fine
            _logger.LogDebug("Task queue channel was already closed during shutdown");
        }

        // Wait for currently executing tasks to complete
        await base.StopAsync(cancellationToken);
        
        _logger.LogInformation("TaskExecutor stopped");
    }
}

/// <summary>
/// Base class for task requests.
/// </summary>
public abstract record TaskRequest(string TaskId);

/// <summary>
/// Task request for downloading an album from Immich.
/// </summary>
public record DownloadTaskRequest(string TaskId, string AlbumId, string AlbumName) : TaskRequest(TaskId);

/// <summary>
/// Task request for resizing images in a downloaded album.
/// </summary>
public record ResizeTaskRequest(string TaskId, int DownloadedAlbumId, int ProfileId) : TaskRequest(TaskId);