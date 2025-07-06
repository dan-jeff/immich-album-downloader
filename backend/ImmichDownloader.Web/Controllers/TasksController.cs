using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ImmichDownloader.Web.Models;
using ImmichDownloader.Web.Models.Requests;
using ImmichDownloader.Web.Services;
using ImmichDownloader.Web.Services.Database;
using System.IO.Compression;

namespace ImmichDownloader.Web.Controllers;

/// <summary>
/// Controller responsible for managing background tasks including downloads, resizes,
/// and providing access to task status and completed downloads.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class TasksController : SecureControllerBase
{
    private readonly ITaskRepository _taskRepository;
    private readonly IDatabaseService _databaseService;
    private readonly TaskExecutor _taskExecutor;
    private readonly ISecureFileService _secureFileService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TasksController"/> class.
    /// </summary>
    /// <param name="taskRepository">The centralized task repository for task operations.</param>
    /// <param name="databaseService">The centralized database service for complex operations.</param>
    /// <param name="taskExecutor">The simplified task executor for task management.</param>
    /// <param name="secureFileService">The secure file service for safe file operations.</param>
    /// <param name="logger">Logger instance for logging operations and errors.</param>
    public TasksController(
        ITaskRepository taskRepository,
        IDatabaseService databaseService,
        TaskExecutor taskExecutor,
        ISecureFileService secureFileService,
        ILogger<TasksController> logger) : base(logger)
    {
        _taskRepository = taskRepository;
        _databaseService = databaseService;
        _taskExecutor = taskExecutor;
        _secureFileService = secureFileService;
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
        // Validate input using secure validation framework
        var validationResult = ValidateInput(request);
        if (validationResult != null)
            return validationResult;

        // Additional validation for album name
        if (!ValidateAlbumName(request.AlbumName))
            return BadRequest(ModelState);

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

        await _taskRepository.CreateTaskAsync(task);

        // Queue the download task using simplified task executor
        var queued = await _taskExecutor.QueueDownloadAsync(taskId, request.AlbumId, request.AlbumName);
        
        if (!queued)
        {
            Logger.LogError("Failed to queue download task {TaskId} - task queue may be full", taskId);
            return CreateErrorResponse(503, "Task queue is currently full. Please try again later.");
        }

        Logger.LogInformation("Download task {TaskId} started for album {AlbumName} by user {Username}", 
            taskId, request.AlbumName, GetCurrentUsername());
        return CreateSuccessResponse(new { task_id = taskId });
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
        // Validate input using secure validation framework
        var validationResult = ValidateInput(request);
        if (validationResult != null)
            return validationResult;

        // Validate IDs
        if (request.DownloadedAlbumId <= 0)
        {
            Logger.LogWarning("Invalid downloaded album ID {AlbumId} provided by user {Username}", 
                request.DownloadedAlbumId, GetCurrentUsername());
            return BadRequest(new { detail = "Invalid downloaded album ID" });
        }

        if (request.ProfileId <= 0)
        {
            Logger.LogWarning("Invalid profile ID {ProfileId} provided by user {Username}", 
                request.ProfileId, GetCurrentUsername());
            return BadRequest(new { detail = "Invalid profile ID" });
        }

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

        await _taskRepository.CreateTaskAsync(task);

        // Queue the resize task using simplified task executor
        var queued = await _taskExecutor.QueueResizeAsync(taskId, request.DownloadedAlbumId, request.ProfileId);
        
        if (!queued)
        {
            Logger.LogError("Failed to queue resize task {TaskId} - task queue may be full", taskId);
            return CreateErrorResponse(503, "Task queue is currently full. Please try again later.");
        }

        Logger.LogInformation("Resize task {TaskId} started for album {AlbumId} with profile {ProfileId} by user {Username}", 
            taskId, request.DownloadedAlbumId, request.ProfileId, GetCurrentUsername());
        return CreateSuccessResponse(new { task_id = taskId });
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
        var tasks = await _databaseService.ExecuteWithScopeAsync(async context =>
        {
            return await context.BackgroundTasks
                .AsNoTracking()
                .OrderByDescending(t => t.CreatedAt)
                .Take(50)
                .ToListAsync();
        });

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
        var downloads = await _databaseService.ExecuteWithScopeAsync(async context =>
        {
            return await context.BackgroundTasks
                .AsNoTracking()
                .Where(t => t.TaskType == TaskType.Resize && 
                           t.Status == Models.TaskStatus.Completed && 
                           (t.ZipData != null || !string.IsNullOrEmpty(t.FilePath)))
                .Join(context.DownloadedAlbums,
                      t => t.DownloadedAlbumId,
                      d => d.Id,
                      (t, d) => new { Task = t, Album = d })
                .Join(context.ResizeProfiles,
                      td => td.Task.ProfileId,
                      p => p.Id,
                      (td, p) => new
                      {
                          id = td.Task.Id,
                          album_name = td.Album.AlbumName,
                          profile_name = p.Name,
                          processed_count = td.Task.ProcessedCount,
                          total_size = td.Task.FileSize,
                          zip_size = td.Task.FileSize,
                          created_at = td.Task.CreatedAt,
                          status = "completed"
                      })
                .OrderByDescending(d => d.created_at)
                .ToListAsync();
        });

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
        Logger.LogError("DEBUG: DownloadZip called for task {TaskId} by user {Username}", taskId, GetCurrentUsername());
        
        // Validate task ID
        if (!ValidateTaskId(taskId))
            return BadRequest(ModelState);

        var taskWithDetails = await _databaseService.ExecuteWithScopeAsync(async context =>
        {
            return await context.BackgroundTasks
                .AsNoTracking()
                .Where(t => t.Id == taskId && t.Status == Models.TaskStatus.Completed)
                .Select(t => new {
                    Task = t,
                    AlbumName = t.TaskType == TaskType.Download ? t.AlbumName :
                        context.DownloadedAlbums.Where(da => da.Id == t.DownloadedAlbumId).Select(da => da.AlbumName).FirstOrDefault(),
                    ProfileName = t.TaskType == TaskType.Resize ? 
                        context.ResizeProfiles.Where(rp => rp.Id == t.ProfileId).Select(rp => rp.Name).FirstOrDefault() : null
                })
                .FirstOrDefaultAsync();
        });

        var task = taskWithDetails?.Task;

        Logger.LogError("DEBUG: Task query result for {TaskId}: Found={TaskFound}, TaskType={TaskType}, FilePath='{FilePath}'", 
            taskId, task != null, task?.TaskType, task?.FilePath);

        if (task == null)
        {
            Logger.LogWarning("Download not found for task {TaskId} by user {Username}", taskId, GetCurrentUsername());
            return CreateErrorResponse(404, "Download not found");
        }

        // Support both streaming (FilePath) and legacy (ZipData) modes
        if (!string.IsNullOrEmpty(task.FilePath))
        {
            // Use the appropriate directory and file path based on task type
            string baseDir;
            string filePath;
            
            if (task.TaskType == TaskType.Resize)
            {
                baseDir = Path.Combine(Path.GetDirectoryName(_secureFileService.GetDownloadDirectory())!, "resized");
                // Extract just the filename from the stored path for resize tasks
                filePath = Path.GetFileName(task.FilePath);
                Logger.LogInformation("Resize task {TaskId}: Using baseDir='{BaseDir}', filePath='{FilePath}' (extracted from '{OriginalPath}')", 
                    taskId, baseDir, filePath, task.FilePath);
            }
            else
            {
                baseDir = _secureFileService.GetDownloadDirectory();
                filePath = task.FilePath;
                Logger.LogInformation("Download task {TaskId}: Using baseDir='{BaseDir}', filePath='{FilePath}'", 
                    taskId, baseDir, filePath);
            }
            
            var fileExists = _secureFileService.FileExists(filePath, baseDir);
            Logger.LogInformation("File existence check for '{FilePath}' in baseDir '{BaseDir}': {FileExists}", 
                filePath, baseDir, fileExists);
            
            if (fileExists)
            {
                try
                {
                    // Streaming mode - return secure file stream
                    var fileStream = _secureFileService.OpenFileStream(filePath, baseDir, 
                        FileMode.Open, FileAccess.Read, FileShare.Read);
                    
                    // Generate descriptive filename with album name and profile
                    var fileName = GenerateDownloadFilename(task.TaskType, taskWithDetails?.AlbumName, taskWithDetails?.ProfileName, taskId);
                    
                    Logger.LogInformation("Serving secure download file {FileName} for task {TaskId} to user {Username}", 
                        fileName, taskId, GetCurrentUsername());
                    return File(fileStream, "application/zip", fileName, enableRangeProcessing: true);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Logger.LogWarning("Unauthorized file access attempt for task {TaskId} by user {Username}: {Error}", 
                        taskId, GetCurrentUsername(), ex.Message);
                    return CreateErrorResponse(403, "File access denied");
                }
            }
        }
        else if (task.ZipData != null)
        {
            // Legacy mode - return byte array
            var fileName = GenerateDownloadFilename(task.TaskType, taskWithDetails?.AlbumName, taskWithDetails?.ProfileName, taskId);
            Logger.LogInformation("Serving legacy download {FileName} for task {TaskId} to user {Username}", 
                fileName, taskId, GetCurrentUsername());
            return File(task.ZipData, "application/zip", fileName);
        }

        Logger.LogWarning("Download file not found for task {TaskId} by user {Username}", taskId, GetCurrentUsername());
        return CreateErrorResponse(404, "Download file not found");
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
        // Validate task ID
        if (!ValidateTaskId(taskId))
            return BadRequest(ModelState);

        var task = await _databaseService.ExecuteWithScopeAsync(async context =>
        {
            return await context.BackgroundTasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.Status == Models.TaskStatus.Completed);
        });

        if (task == null)
        {
            Logger.LogWarning("Download not found for deletion: task {TaskId} by user {Username}", taskId, GetCurrentUsername());
            return CreateErrorResponse(404, "Download not found");
        }

        // Delete file if it exists (streaming mode) using secure file service
        if (!string.IsNullOrEmpty(task.FilePath))
        {
            try
            {
                // Use the appropriate directory and file path based on task type
                string baseDir;
                string filePath;
                
                if (task.TaskType == TaskType.Resize)
                {
                    baseDir = Path.Combine(Path.GetDirectoryName(_secureFileService.GetDownloadDirectory())!, "resized");
                    // Extract just the filename from the stored path for resize tasks
                    filePath = Path.GetFileName(task.FilePath);
                }
                else
                {
                    baseDir = _secureFileService.GetDownloadDirectory();
                    filePath = task.FilePath;
                }
                
                var deleted = _secureFileService.DeleteFile(filePath, baseDir);
                
                if (deleted)
                {
                    Logger.LogInformation("Securely deleted file {FilePath} for task {TaskId} by user {Username}", 
                        task.FilePath, taskId, GetCurrentUsername());
                }
                else
                {
                    Logger.LogWarning("File {FilePath} not found for deletion for task {TaskId} by user {Username}", 
                        task.FilePath, taskId, GetCurrentUsername());
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.LogWarning("Unauthorized file deletion attempt for task {TaskId} by user {Username}: {Error}", 
                    taskId, GetCurrentUsername(), ex.Message);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error securely deleting file {FilePath} for task {TaskId} by user {Username}", 
                    task.FilePath, taskId, GetCurrentUsername());
            }
        }

        await _taskRepository.DeleteTaskAsync(taskId);

        Logger.LogInformation("Download task {TaskId} deleted by user {Username}", taskId, GetCurrentUsername());
        return CreateSuccessResponse(new { success = true });
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
        // Validate task ID
        if (!ValidateTaskId(taskId))
            return BadRequest(ModelState);

        var task = await _taskRepository.GetTaskAsync(taskId);

        if (task == null)
        {
            Logger.LogWarning("Task not found for deletion: {TaskId} by user {Username}", taskId, GetCurrentUsername());
            return CreateErrorResponse(404, "Task not found");
        }

        // Don't allow deletion of running tasks
        if (task.Status == Models.TaskStatus.InProgress)
        {
            Logger.LogWarning("Attempt to delete running task {TaskId} by user {Username}", taskId, GetCurrentUsername());
            return CreateErrorResponse(400, "Cannot delete running task");
        }

        try
        {
            var deleted = await _taskRepository.DeleteTaskAsync(taskId);
            
            if (deleted)
            {
                Logger.LogInformation("Task {TaskId} deleted by user {Username}", taskId, GetCurrentUsername());
                return CreateSuccessResponse(new { success = true });
            }
            else
            {
                Logger.LogWarning("Task {TaskId} could not be deleted by user {Username}", taskId, GetCurrentUsername());
                return CreateErrorResponse(500, "Failed to delete task");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete task {TaskId} by user {Username}: {Error}", 
                taskId, GetCurrentUsername(), ex.Message);
            return CreateErrorResponse(500, "Failed to delete task", new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generates a descriptive filename for download ZIP files.
    /// </summary>
    /// <param name="taskType">The type of task (Download or Resize).</param>
    /// <param name="albumName">The name of the album.</param>
    /// <param name="profileName">The name of the resize profile (for resize tasks).</param>
    /// <param name="taskId">The task ID as fallback.</param>
    /// <returns>A sanitized filename for the ZIP download.</returns>
    private string GenerateDownloadFilename(TaskType taskType, string? albumName, string? profileName, string taskId)
    {
        var sanitizedAlbumName = SanitizeFilename(albumName) ?? "Unknown_Album";
        
        if (taskType == TaskType.Resize && !string.IsNullOrEmpty(profileName))
        {
            var sanitizedProfileName = SanitizeFilename(profileName) ?? "Unknown_Profile";
            return $"{sanitizedAlbumName}_{sanitizedProfileName}.zip";
        }
        else if (taskType == TaskType.Download)
        {
            return $"{sanitizedAlbumName}_Original.zip";
        }
        
        // Fallback to old naming scheme
        return taskType == TaskType.Download ? $"download_{taskId}.zip" : $"resized_{taskId}.zip";
    }

    /// <summary>
    /// Sanitizes a string to be safe for use as a filename.
    /// </summary>
    /// <param name="input">The input string to sanitize.</param>
    /// <returns>A sanitized filename or null if input is null/empty.</returns>
    private string? SanitizeFilename(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        // Remove or replace invalid filename characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = input;
        
        foreach (var invalidChar in invalidChars)
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }
        
        // Replace common problematic characters
        sanitized = sanitized
            .Replace(' ', '_')           // Spaces to underscores
            .Replace(".", "_")           // Dots to underscores (except for extension)
            .Replace(",", "_")           // Commas to underscores
            .Replace("'", "")            // Remove apostrophes
            .Replace("\"", "")           // Remove quotes
            .Trim('_');                  // Remove leading/trailing underscores
        
        // Limit length and ensure it's not empty
        if (sanitized.Length > 50)
            sanitized = sanitized.Substring(0, 50).Trim('_');
            
        return string.IsNullOrEmpty(sanitized) ? null : sanitized;
    }
}
