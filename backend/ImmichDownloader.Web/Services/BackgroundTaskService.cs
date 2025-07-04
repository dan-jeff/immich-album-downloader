namespace ImmichDownloader.Web.Services;

/// <summary>
/// Background service that processes queued background tasks.
/// This service runs continuously, dequeuing and executing background tasks from the task queue.
/// Each task is executed in its own service scope to ensure proper dependency injection.
/// </summary>
public class BackgroundTaskService : BackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundTaskService> _logger;

    /// <summary>
    /// Initializes a new instance of the BackgroundTaskService class.
    /// </summary>
    /// <param name="taskQueue">The background task queue for retrieving tasks to execute.</param>
    /// <param name="serviceProvider">The service provider for creating service scopes.</param>
    /// <param name="logger">The logger instance for this service.</param>
    public BackgroundTaskService(
        IBackgroundTaskQueue taskQueue,
        IServiceProvider serviceProvider,
        ILogger<BackgroundTaskService> logger)
    {
        _taskQueue = taskQueue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// The main execution method of the background service.
    /// Continuously dequeues and executes background tasks until the service is stopped.
    /// Each task is executed in its own service scope to ensure proper dependency injection and resource cleanup.
    /// </summary>
    /// <param name="stoppingToken">A cancellation token that indicates when the service should stop.</param>
    /// <returns>A task that represents the asynchronous execution of the background service.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Task Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await _taskQueue.DequeueAsync(stoppingToken);

            if (workItem != null)
            {
                try
                {
                    // Create a service scope to access scoped services
                    using var scope = _serviceProvider.CreateScope();
                    await workItem(scope.ServiceProvider, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing background task.");
                }
            }
        }

        _logger.LogInformation("Background Task Service is stopping.");
    }
}