using ImmichDownloader.Web.Models;

namespace ImmichDownloader.Web.Services;

/// <summary>
/// Service interface for tracking and notifying task progress via SignalR.
/// Provides methods for sending real-time progress updates to connected clients.
/// </summary>
public interface ITaskProgressService
{
    /// <summary>
    /// Notifies connected clients about task progress updates.
    /// This method sends real-time progress information via SignalR to update the UI.
    /// </summary>
    /// <param name="taskId">The unique identifier of the task being updated.</param>
    /// <param name="taskType">The type of task (Download, Resize, etc.).</param>
    /// <param name="status">The current status of the task.</param>
    /// <param name="progress">The current progress value (default is 0).</param>
    /// <param name="total">The total value for progress calculations (default is 0).</param>
    /// <param name="message">Optional message describing the current operation.</param>
    /// <returns>A task that represents the asynchronous notification operation.</returns>
    /// <exception cref="ArgumentException">Thrown when taskId is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the SignalR hub context is not available.</exception>
    Task NotifyProgressAsync(string taskId, TaskType taskType, Models.TaskStatus status, int progress = 0, int total = 0, string? message = null);

    /// <summary>
    /// Notifies connected clients that a task has completed successfully.
    /// This method sends a completion notification via SignalR to update the UI.
    /// </summary>
    /// <param name="taskId">The unique identifier of the completed task.</param>
    /// <param name="taskType">The type of task that completed.</param>
    /// <returns>A task that represents the asynchronous notification operation.</returns>
    /// <exception cref="ArgumentException">Thrown when taskId is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the SignalR hub context is not available.</exception>
    Task NotifyTaskCompletedAsync(string taskId, TaskType taskType);

    /// <summary>
    /// Notifies connected clients that a task has encountered an error.
    /// This method sends an error notification via SignalR to update the UI.
    /// </summary>
    /// <param name="taskId">The unique identifier of the failed task.</param>
    /// <param name="taskType">The type of task that failed.</param>
    /// <param name="error">The error message describing what went wrong.</param>
    /// <returns>A task that represents the asynchronous notification operation.</returns>
    /// <exception cref="ArgumentException">Thrown when taskId or error is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the SignalR hub context is not available.</exception>
    Task NotifyTaskErrorAsync(string taskId, TaskType taskType, string error);
}

/// <summary>
/// Represents a task progress update that can be sent to clients via SignalR.
/// Contains all the information needed to update the UI about task progress.
/// </summary>
public class TaskProgressUpdate
{
    /// <summary>
    /// Gets or sets the unique identifier of the task.
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of task (Download, Resize, etc.).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current status of the task.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current progress value.
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// Gets or sets the total value for progress calculations.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets an optional message describing the current operation.
    /// </summary>
    public string? Message { get; set; }
}