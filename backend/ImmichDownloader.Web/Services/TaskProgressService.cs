using Microsoft.AspNetCore.SignalR;
using ImmichDownloader.Web.Hubs;
using ImmichDownloader.Web.Models;

namespace ImmichDownloader.Web.Services;

/// <summary>
/// Service responsible for tracking and notifying clients about background task progress
/// using SignalR real-time communication.
/// </summary>
public class TaskProgressService : ITaskProgressService
{
    private readonly IHubContext<ProgressHub> _hubContext;
    private readonly ILogger<TaskProgressService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskProgressService"/> class.
    /// </summary>
    /// <param name="hubContext">The SignalR hub context for sending real-time notifications.</param>
    /// <param name="logger">Logger instance for logging operations and errors.</param>
    public TaskProgressService(IHubContext<ProgressHub> hubContext, ILogger<TaskProgressService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Sends a progress update notification to all connected clients via SignalR.
    /// </summary>
    /// <param name="taskId">The unique identifier for the task being updated.</param>
    /// <param name="taskType">The type of task (Download, Resize, etc.).</param>
    /// <param name="status">The current status of the task.</param>
    /// <param name="progress">The current progress value (default: 0).</param>
    /// <param name="total">The total number of items to process (default: 0).</param>
    /// <param name="message">Optional message describing the current operation.</param>
    /// <returns>A task representing the asynchronous notification operation.</returns>
    public async Task NotifyProgressAsync(string taskId, TaskType taskType, Models.TaskStatus status, int progress = 0, int total = 0, string? message = null)
    {
        try
        {
            var update = new TaskProgressUpdate
            {
                TaskId = taskId,
                Type = taskType.ToString().ToLowerInvariant(),
                Status = status.ToString().ToLowerInvariant(),
                Progress = progress,
                Total = total,
                Message = message
            };

            await _hubContext.Clients.Group("ProgressUpdates").SendAsync("TaskProgress", update);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending progress notification for task {TaskId}", taskId);
        }
    }

    /// <summary>
    /// Sends a completion notification for a successfully completed task.
    /// </summary>
    /// <param name="taskId">The unique identifier for the completed task.</param>
    /// <param name="taskType">The type of task that was completed.</param>
    /// <returns>A task representing the asynchronous notification operation.</returns>
    public async Task NotifyTaskCompletedAsync(string taskId, TaskType taskType)
    {
        await NotifyProgressAsync(taskId, taskType, Models.TaskStatus.Completed, 0, 0, "Task completed successfully");
    }

    /// <summary>
    /// Sends an error notification for a failed task.
    /// </summary>
    /// <param name="taskId">The unique identifier for the failed task.</param>
    /// <param name="taskType">The type of task that failed.</param>
    /// <param name="error">The error message describing what went wrong.</param>
    /// <returns>A task representing the asynchronous notification operation.</returns>
    public async Task NotifyTaskErrorAsync(string taskId, TaskType taskType, string error)
    {
        await NotifyProgressAsync(taskId, taskType, Models.TaskStatus.Error, 0, 0, $"Error: {error}");
    }
}