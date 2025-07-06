using ImmichDownloader.Web.Models;

namespace ImmichDownloader.Web.Services.Database;

/// <summary>
/// Repository interface for managing background tasks in the database.
/// Provides centralized task management operations with consistent error handling.
/// </summary>
public interface ITaskRepository
{
    /// <summary>
    /// Gets a background task by its ID.
    /// </summary>
    /// <param name="taskId">The unique task identifier.</param>
    /// <returns>The background task or null if not found.</returns>
    Task<BackgroundTask?> GetTaskAsync(string taskId);

    /// <summary>
    /// Gets multiple background tasks by their IDs.
    /// </summary>
    /// <param name="taskIds">The task identifiers to retrieve.</param>
    /// <returns>Dictionary mapping task IDs to background tasks.</returns>
    Task<Dictionary<string, BackgroundTask>> GetTasksAsync(params string[] taskIds);

    /// <summary>
    /// Creates a new background task.
    /// </summary>
    /// <param name="task">The background task to create.</param>
    Task CreateTaskAsync(BackgroundTask task);

    /// <summary>
    /// Updates the status of a background task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="status">The new task status.</param>
    /// <param name="message">Optional status message.</param>
    /// <param name="progress">Optional progress value.</param>
    /// <param name="total">Optional total value for progress calculation.</param>
    Task UpdateTaskStatusAsync(string taskId, Models.TaskStatus status, string? message = null, int? progress = null, int? total = null);

    /// <summary>
    /// Updates multiple fields of a background task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="updates">Action to perform updates on the task.</param>
    Task UpdateTaskAsync(string taskId, Action<BackgroundTask> updates);

    /// <summary>
    /// Marks a task as completed.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="message">Optional completion message.</param>
    Task CompleteTaskAsync(string taskId, string? message = null);

    /// <summary>
    /// Marks a task as failed with an error message.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="errorMessage">The error message.</param>
    Task FailTaskAsync(string taskId, string errorMessage);

    /// <summary>
    /// Gets all active tasks (pending or in progress).
    /// </summary>
    /// <param name="limit">Maximum number of tasks to return.</param>
    /// <returns>List of active background tasks.</returns>
    Task<List<BackgroundTask>> GetActiveTasksAsync(int limit = 50);

    /// <summary>
    /// Gets tasks by status.
    /// </summary>
    /// <param name="status">The task status to filter by.</param>
    /// <param name="limit">Maximum number of tasks to return.</param>
    /// <returns>List of background tasks with the specified status.</returns>
    Task<List<BackgroundTask>> GetTasksByStatusAsync(Models.TaskStatus status, int limit = 50);

    /// <summary>
    /// Gets tasks by type.
    /// </summary>
    /// <param name="taskType">The task type to filter by.</param>
    /// <param name="limit">Maximum number of tasks to return.</param>
    /// <returns>List of background tasks with the specified type.</returns>
    Task<List<BackgroundTask>> GetTasksByTypeAsync(TaskType taskType, int limit = 50);

    /// <summary>
    /// Gets completed tasks (successful downloads/resizes).
    /// </summary>
    /// <param name="limit">Maximum number of tasks to return.</param>
    /// <returns>List of completed background tasks.</returns>
    Task<List<BackgroundTask>> GetCompletedTasksAsync(int limit = 50);

    /// <summary>
    /// Gets recent tasks within a specified time range.
    /// </summary>
    /// <param name="since">Start time for the range.</param>
    /// <param name="limit">Maximum number of tasks to return.</param>
    /// <returns>List of background tasks created since the specified time.</returns>
    Task<List<BackgroundTask>> GetRecentTasksAsync(DateTime since, int limit = 50);

    /// <summary>
    /// Deletes a background task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <returns>True if the task was deleted, false if it didn't exist.</returns>
    Task<bool> DeleteTaskAsync(string taskId);

    /// <summary>
    /// Deletes old completed tasks to maintain database size.
    /// </summary>
    /// <param name="olderThan">Delete tasks completed before this date.</param>
    /// <returns>Number of tasks deleted.</returns>
    Task<int> CleanupOldTasksAsync(DateTime olderThan);

    /// <summary>
    /// Gets task statistics.
    /// </summary>
    /// <returns>Dictionary containing task counts by status.</returns>
    Task<Dictionary<Models.TaskStatus, int>> GetTaskStatisticsAsync();

    /// <summary>
    /// Checks if a task exists.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <returns>True if the task exists, false otherwise.</returns>
    Task<bool> TaskExistsAsync(string taskId);

    /// <summary>
    /// Updates the processed count for a task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="processedCount">The number of items processed.</param>
    Task UpdateProcessedCountAsync(string taskId, int processedCount);
}