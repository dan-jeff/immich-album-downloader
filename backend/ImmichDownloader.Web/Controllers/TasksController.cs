using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using ImmichDownloader.Web.Models.Requests;
using ImmichDownloader.Web.Services;
using System.IO.Compression;

namespace ImmichDownloader.Web.Controllers;

/// <summary>
/// Controller responsible for managing background tasks including downloads, resizes,
/// and providing access to task status and completed downloads.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger<TasksController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TasksController"/> class.
    /// </summary>
    /// <param name="context">The database context for task operations.</param>
    /// <param name="taskQueue">The background task queue for queueing work.</param>
    /// <param name="logger">Logger instance for logging operations and errors.</param>
    public TasksController(
        ApplicationDbContext context,
        IBackgroundTaskQueue taskQueue,
        ILogger<TasksController> logger)
    {
        _context = context;
        _taskQueue = taskQueue;
        _logger = logger;
    }

    /// <summary>
    /// Starts a new album download task using the streaming download service.
    /// </summary>
    /// <param name="request">The download request containing album information.</param>
    /// <returns>A response containing the task ID for tracking progress.</returns>
    /// <response code="200">Download task started successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">Unauthorized access.</response>
    [HttpPost("download")]
    public async Task<IActionResult> StartDownload([FromBody] DownloadRequest request)
    {
        var taskId = Guid.NewGuid().ToString();

        // Create task record
        var task = new BackgroundTask
        {
            Id = taskId,
            TaskType = TaskType.Download,
            Status = Models.TaskStatus.Pending,
            AlbumId = request.AlbumId,
            AlbumName = request.AlbumName,
            Total = 0
        };

        _context.BackgroundTasks.Add(task);
        await _context.SaveChangesAsync();

        // Queue the download task using streaming service
        await _taskQueue.QueueBackgroundTaskAsync(async (serviceProvider, cancellationToken) =>
        {
            var streamingDownloadService = serviceProvider.GetRequiredService<IStreamingDownloadService>();
            await streamingDownloadService.StartDownloadAsync(taskId, request.AlbumId, request.AlbumName, cancellationToken);
        });

        return Ok(new { task_id = taskId });
    }

    /// <summary>
    /// Starts a new image resize task using the streaming resize service.
    /// </summary>
    /// <param name="request">The resize request containing album and profile information.</param>
    /// <returns>A response containing the task ID for tracking progress.</returns>
    /// <response code="200">Resize task started successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">Unauthorized access.</response>
    [HttpPost("resize")]
    public async Task<IActionResult> StartResize([FromBody] ResizeRequest request)
    {
        var taskId = Guid.NewGuid().ToString();

        // Create task record
        var task = new BackgroundTask
        {
            Id = taskId,
            TaskType = TaskType.Resize,
            Status = Models.TaskStatus.Pending,
            DownloadedAlbumId = request.DownloadedAlbumId,
            ProfileId = request.ProfileId,
            Total = 0
        };

        _context.BackgroundTasks.Add(task);
        await _context.SaveChangesAsync();

        // Queue the resize task using streaming service
        await _taskQueue.QueueBackgroundTaskAsync(async (serviceProvider, cancellationToken) =>
        {
            var streamingResizeService = serviceProvider.GetRequiredService<IStreamingResizeService>();
            await streamingResizeService.StartResizeAsync(taskId, request.DownloadedAlbumId, request.ProfileId, cancellationToken);
        });

        return Ok(new { task_id = taskId });
    }

    /// <summary>
    /// Retrieves the most recent background tasks with their current status and progress.
    /// </summary>
    /// <returns>A list of active tasks with their details.</returns>
    /// <response code="200">Returns the list of active tasks.</response>
    /// <response code="401">Unauthorized access.</response>
    [HttpGet("tasks")]
    public async Task<IActionResult> GetActiveTasks()
    {
        var tasks = await _context.BackgroundTasks
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .ToListAsync();

        var result = tasks.Select(t => new
        {
            id = t.Id,
            type = t.TaskType.ToString().ToLowerInvariant(),
            status = t.Status switch
            {
                Models.TaskStatus.Pending => "pending",
                Models.TaskStatus.InProgress => "in_progress",
                Models.TaskStatus.Completed => "completed",
                Models.TaskStatus.Error => "failed",
                _ => t.Status.ToString().ToLowerInvariant()
            },
            progress = t.Progress,
            total = t.Total,
            message = t.CurrentStep,
            created_at = DateTime.SpecifyKind(t.CreatedAt, DateTimeKind.Utc),
            updated_at = t.CompletedAt.HasValue ? DateTime.SpecifyKind(t.CompletedAt.Value, DateTimeKind.Utc) : DateTime.SpecifyKind(t.CreatedAt, DateTimeKind.Utc)
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Retrieves all completed resize tasks that have downloadable ZIP files.
    /// </summary>
    /// <returns>A list of completed downloads with metadata.</returns>
    /// <response code="200">Returns the list of completed downloads.</response>
    /// <response code="401">Unauthorized access.</response>
    [HttpGet("downloads")]
    public async Task<IActionResult> GetCompletedDownloads()
    {
        var downloads = await _context.BackgroundTasks
            .Where(t => t.TaskType == TaskType.Resize && 
                       t.Status == Models.TaskStatus.Completed && 
                       t.ZipData != null)
            .Join(_context.DownloadedAlbums,
                  t => t.DownloadedAlbumId,
                  d => d.Id,
                  (t, d) => new { Task = t, Album = d })
            .Join(_context.ResizeProfiles,
                  td => td.Task.ProfileId,
                  p => p.Id,
                  (td, p) => new
                  {
                      id = td.Task.Id,
                      album_name = td.Album.AlbumName,
                      profile_name = p.Name,
                      processed_count = td.Task.ProcessedCount,
                      total_size = td.Task.ZipSize,
                      zip_size = td.Task.ZipSize,
                      created_at = td.Task.CreatedAt,
                      status = "completed"
                  })
            .OrderByDescending(d => d.created_at)
            .ToListAsync();

        return Ok(downloads);
    }

    /// <summary>
    /// Downloads the ZIP file for a completed task.
    /// Supports both streaming (file-based) and legacy (memory-based) download modes.
    /// </summary>
    /// <param name="taskId">The unique identifier of the completed task.</param>
    /// <returns>The ZIP file containing the processed images.</returns>
    /// <response code="200">Returns the ZIP file stream.</response>
    /// <response code="404">Task not found or no download available.</response>
    /// <response code="401">Unauthorized access.</response>
    [HttpGet("downloads/{taskId}")]
    public async Task<IActionResult> DownloadZip(string taskId)
    {
        var task = await _context.BackgroundTasks
            .FirstOrDefaultAsync(t => t.Id == taskId && t.Status == Models.TaskStatus.Completed);

        if (task == null)
            return NotFound(new { detail = "Download not found" });

        // Support both streaming (FilePath) and legacy (ZipData) modes
        if (!string.IsNullOrEmpty(task.FilePath) && System.IO.File.Exists(task.FilePath))
        {
            // Streaming mode - return file stream
            var fileStream = new FileStream(task.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fileName = task.TaskType == TaskType.Download ? $"download_{taskId}.zip" : $"resized_{taskId}.zip";
            
            return File(fileStream, "application/zip", fileName, enableRangeProcessing: true);
        }
        else if (task.ZipData != null)
        {
            // Legacy mode - return byte array
            var fileName = task.TaskType == TaskType.Download ? $"download_{taskId}.zip" : $"resized_{taskId}.zip";
            return File(task.ZipData, "application/zip", fileName);
        }

        return NotFound(new { detail = "Download file not found" });
    }

    /// <summary>
    /// Deletes a completed download task and its associated files.
    /// </summary>
    /// <param name="taskId">The unique identifier of the task to delete.</param>
    /// <returns>A response indicating success or failure.</returns>
    /// <response code="200">Download deleted successfully.</response>
    /// <response code="404">Download not found.</response>
    /// <response code="401">Unauthorized access.</response>
    [HttpDelete("downloads/{taskId}")]
    public async Task<IActionResult> DeleteDownload(string taskId)
    {
        var task = await _context.BackgroundTasks
            .FirstOrDefaultAsync(t => t.Id == taskId && t.Status == Models.TaskStatus.Completed);

        if (task == null)
            return NotFound(new { detail = "Download not found" });

        // Delete file if it exists (streaming mode)
        if (!string.IsNullOrEmpty(task.FilePath) && System.IO.File.Exists(task.FilePath))
        {
            try
            {
                System.IO.File.Delete(task.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {FilePath} for task {TaskId}", task.FilePath, taskId);
            }
        }

        _context.BackgroundTasks.Remove(task);
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }

    /// <summary>
    /// Deletes a background task. Running tasks cannot be deleted.
    /// </summary>
    /// <param name="taskId">The unique identifier of the task to delete.</param>
    /// <returns>A response indicating success or failure.</returns>
    /// <response code="200">Task deleted successfully.</response>
    /// <response code="400">Cannot delete running task.</response>
    /// <response code="404">Task not found.</response>
    /// <response code="500">Internal server error.</response>
    /// <response code="401">Unauthorized access.</response>
    [HttpDelete("tasks/{taskId}")]
    public async Task<IActionResult> DeleteTask(string taskId)
    {
        var task = await _context.BackgroundTasks
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null)
            return NotFound(new { detail = "Task not found" });

        // Don't allow deletion of running tasks
        if (task.Status == Models.TaskStatus.InProgress)
            return BadRequest(new { detail = "Cannot delete running task" });

        try
        {
            _context.BackgroundTasks.Remove(task);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete task {TaskId}: {Error}", taskId, ex.Message);
            return StatusCode(500, new { detail = "Failed to delete task", error = ex.Message });
        }
    }
}
