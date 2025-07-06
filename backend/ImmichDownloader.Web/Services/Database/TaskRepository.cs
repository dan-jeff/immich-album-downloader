using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace ImmichDownloader.Web.Services.Database;

/// <summary>
/// Implementation of task repository that provides centralized background task management
/// with consistent database access patterns and proper error handling.
/// </summary>
public class TaskRepository : ITaskRepository
{
    private readonly IDatabaseService _databaseService;
    private readonly ILogger<TaskRepository> _logger;

    public TaskRepository(IDatabaseService databaseService, ILogger<TaskRepository> logger)
    {
        _databaseService = databaseService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BackgroundTask?> GetTaskAsync(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("Task ID cannot be empty", nameof(taskId));

        return await _databaseService.ExecuteWithScopeAsync(async context =>
        {
            return await context.BackgroundTasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == taskId);
        });
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, BackgroundTask>> GetTasksAsync(params string[] taskIds)
    {
        if (taskIds == null || taskIds.Length == 0)
            return new Dictionary<string, BackgroundTask>();

        return await _databaseService.ExecuteWithScopeAsync(async context =>
        {
            var tasks = await context.BackgroundTasks
                .AsNoTracking()
                .Where(t => taskIds.Contains(t.Id))
                .ToListAsync();

            return tasks.ToDictionary(t => t.Id, t => t);
        });
    }

    /// <inheritdoc />
    public async Task CreateTaskAsync(BackgroundTask task)
    {
        if (task == null)
            throw new ArgumentNullException(nameof(task));

        await _databaseService.ExecuteInTransactionAsync(async context =>
        {
            // Ensure timestamps are set
            if (task.CreatedAt == default)
                task.CreatedAt = DateTime.UtcNow;

            context.BackgroundTasks.Add(task);
            await context.SaveChangesAsync();

            _logger.LogDebug("Created background task {TaskId} of type {TaskType}", task.Id, task.TaskType);
        });
    }

    /// <inheritdoc />
    public async Task UpdateTaskStatusAsync(string taskId, Models.TaskStatus status, string? message = null, int? progress = null, int? total = null)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("Task ID cannot be empty", nameof(taskId));

        await _databaseService.ExecuteInTransactionAsync(async context =>
        {
            var task = await context.BackgroundTasks
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null)
            {
                _logger.LogWarning("Attempted to update non-existent task {TaskId}", taskId);
                return;
            }

            var oldStatus = task.Status;
            task.Status = status;

            if (message != null)
                task.CurrentStep = message;

            if (progress.HasValue)
                task.Progress = progress.Value;

            if (total.HasValue)
                task.Total = total.Value;

            // Set completion time for terminal states
            if (status == Models.TaskStatus.Completed || status == Models.TaskStatus.Error)
            {
                task.CompletedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync();

            _logger.LogDebug("Updated task {TaskId} status from {OldStatus} to {NewStatus}", 
                taskId, oldStatus, status);
        });
    }

    /// <inheritdoc />
    public async Task UpdateTaskAsync(string taskId, Action<BackgroundTask> updates)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("Task ID cannot be empty", nameof(taskId));
        if (updates == null)
            throw new ArgumentNullException(nameof(updates));

        await _databaseService.ExecuteInTransactionAsync(async context =>
        {
            var task = await context.BackgroundTasks
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null)
            {
                _logger.LogWarning("Attempted to update non-existent task {TaskId}", taskId);
                return;
            }

            updates(task);
            await context.SaveChangesAsync();

            _logger.LogDebug("Updated task {TaskId}", taskId);
        });
    }

    /// <inheritdoc />
    public async Task CompleteTaskAsync(string taskId, string? message = null)
    {
        await UpdateTaskStatusAsync(taskId, Models.TaskStatus.Completed, message);
        _logger.LogInformation("Task {TaskId} completed successfully", taskId);
    }

    /// <inheritdoc />
    public async Task FailTaskAsync(string taskId, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be empty", nameof(errorMessage));

        await UpdateTaskStatusAsync(taskId, Models.TaskStatus.Error, errorMessage);
        _logger.LogWarning("Task {TaskId} failed: {ErrorMessage}", taskId, errorMessage);
    }

    /// <inheritdoc />
    public async Task<List<BackgroundTask>> GetActiveTasksAsync(int limit = 50)
    {
        return await _databaseService.ExecuteWithScopeAsync(async context =>
        {
            return await context.BackgroundTasks
                .AsNoTracking()
                .Where(t => t.Status == Models.TaskStatus.Pending || t.Status == Models.TaskStatus.InProgress)
                .OrderBy(t => t.CreatedAt)
                .Take(limit)
                .ToListAsync();
        });
    }

    /// <inheritdoc />
    public async Task<List<BackgroundTask>> GetTasksByStatusAsync(Models.TaskStatus status, int limit = 50)
    {
        return await _databaseService.ExecuteWithScopeAsync(async context =>
        {
            return await context.BackgroundTasks
                .AsNoTracking()
                .Where(t => t.Status == status)
                .OrderByDescending(t => t.CreatedAt)
                .Take(limit)
                .ToListAsync();
        });
    }

    /// <inheritdoc />
    public async Task<List<BackgroundTask>> GetTasksByTypeAsync(TaskType taskType, int limit = 50)
    {
        return await _databaseService.ExecuteWithScopeAsync(async context =>
        {
            return await context.BackgroundTasks
                .AsNoTracking()
                .Where(t => t.TaskType == taskType)
                .OrderByDescending(t => t.CreatedAt)
                .Take(limit)
                .ToListAsync();
        });
    }

    /// <inheritdoc />
    public async Task<List<BackgroundTask>> GetCompletedTasksAsync(int limit = 50)
    {
        return await GetTasksByStatusAsync(Models.TaskStatus.Completed, limit);
    }

    /// <inheritdoc />
    public async Task<List<BackgroundTask>> GetRecentTasksAsync(DateTime since, int limit = 50)
    {
        return await _databaseService.ExecuteWithScopeAsync(async context =>
        {
            return await context.BackgroundTasks
                .AsNoTracking()
                .Where(t => t.CreatedAt >= since)
                .OrderByDescending(t => t.CreatedAt)
                .Take(limit)
                .ToListAsync();
        });
    }

    /// <inheritdoc />
    public async Task<bool> DeleteTaskAsync(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("Task ID cannot be empty", nameof(taskId));

        return await _databaseService.ExecuteInTransactionAsync(async context =>
        {
            var task = await context.BackgroundTasks
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null)
                return false;

            context.BackgroundTasks.Remove(task);
            await context.SaveChangesAsync();

            _logger.LogDebug("Deleted task {TaskId}", taskId);
            return true;
        });
    }

    /// <inheritdoc />
    public async Task<int> CleanupOldTasksAsync(DateTime olderThan)
    {
        return await _databaseService.ExecuteInTransactionAsync(async context =>
        {
            var oldTasks = await context.BackgroundTasks
                .Where(t => (t.Status == Models.TaskStatus.Completed || t.Status == Models.TaskStatus.Error) 
                           && t.CompletedAt.HasValue 
                           && t.CompletedAt.Value < olderThan)
                .ToListAsync();

            if (oldTasks.Count == 0)
                return 0;

            context.BackgroundTasks.RemoveRange(oldTasks);
            await context.SaveChangesAsync();

            _logger.LogInformation("Cleaned up {Count} old tasks completed before {Date}", 
                oldTasks.Count, olderThan);

            return oldTasks.Count;
        });
    }

    /// <inheritdoc />
    public async Task<Dictionary<Models.TaskStatus, int>> GetTaskStatisticsAsync()
    {
        return await _databaseService.ExecuteWithScopeAsync(async context =>
        {
            var stats = await context.BackgroundTasks
                .AsNoTracking()
                .GroupBy(t => t.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            return stats.ToDictionary(s => s.Status, s => s.Count);
        });
    }

    /// <inheritdoc />
    public async Task<bool> TaskExistsAsync(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("Task ID cannot be empty", nameof(taskId));

        return await _databaseService.ExecuteWithScopeAsync(async context =>
        {
            return await context.BackgroundTasks
                .AsNoTracking()
                .AnyAsync(t => t.Id == taskId);
        });
    }

    /// <inheritdoc />
    public async Task UpdateProcessedCountAsync(string taskId, int processedCount)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("Task ID cannot be empty", nameof(taskId));
        if (processedCount < 0)
            throw new ArgumentException("Processed count cannot be negative", nameof(processedCount));

        await UpdateTaskAsync(taskId, task =>
        {
            task.ProcessedCount = processedCount;
            
            // Update progress if total is set
            if (task.Total > 0)
            {
                task.Progress = (int)Math.Round((double)processedCount / task.Total * 100);
            }
        });
    }
}