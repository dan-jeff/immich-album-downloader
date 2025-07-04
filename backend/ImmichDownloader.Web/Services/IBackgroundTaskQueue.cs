using System.Threading.Channels;

namespace ImmichDownloader.Web.Services;

/// <summary>
/// Interface for a background task queue that manages the execution of background work items.
/// Provides methods for queuing and dequeuing background tasks in a thread-safe manner.
/// </summary>
public interface IBackgroundTaskQueue
{
    /// <summary>
    /// Queues a background task for execution.
    /// The task will be executed by the background service when resources are available.
    /// </summary>
    /// <param name="workItem">A function that represents the work to be executed. The function receives an IServiceProvider and CancellationToken.</param>
    /// <returns>A task that represents the asynchronous queue operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when workItem is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the queue is full and cannot accept more items.</exception>
    Task QueueBackgroundTaskAsync(Func<IServiceProvider, CancellationToken, Task> workItem);

    /// <summary>
    /// Dequeues a background task from the queue for execution.
    /// This method blocks until a task is available or the cancellation token is triggered.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the dequeue operation.</param>
    /// <returns>
    /// A task that represents the asynchronous dequeue operation. The task result contains the work item function if available, null otherwise.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    Task<Func<IServiceProvider, CancellationToken, Task>?> DequeueAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Implementation of the background task queue using .NET Channels for thread-safe task management.
/// This class provides a bounded queue that can hold a specified number of background tasks.
/// </summary>
public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    /// <summary>
    /// The underlying channel used for storing and retrieving background tasks.
    /// </summary>
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _queue;

    /// <summary>
    /// Initializes a new instance of the BackgroundTaskQueue class with the specified capacity.
    /// </summary>
    /// <param name="capacity">The maximum number of tasks that can be queued. Default is 100.</param>
    public BackgroundTaskQueue(int capacity = 100)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, Task>>(options);
    }

    /// <summary>
    /// Queues a background task for execution.
    /// The task will be executed by the background service when resources are available.
    /// </summary>
    /// <param name="workItem">A function that represents the work to be executed. The function receives an IServiceProvider and CancellationToken.</param>
    /// <returns>A task that represents the asynchronous queue operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when workItem is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the queue is full and cannot accept more items.</exception>
    public async Task QueueBackgroundTaskAsync(Func<IServiceProvider, CancellationToken, Task> workItem)
    {
        if (workItem == null)
            throw new ArgumentNullException(nameof(workItem));

        await _queue.Writer.WriteAsync(workItem);
    }

    /// <summary>
    /// Dequeues a background task from the queue for execution.
    /// This method blocks until a task is available or the cancellation token is triggered.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the dequeue operation.</param>
    /// <returns>
    /// A task that represents the asynchronous dequeue operation. The task result contains the work item function if available, null otherwise.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    public async Task<Func<IServiceProvider, CancellationToken, Task>?> DequeueAsync(CancellationToken cancellationToken)
    {
        var workItem = await _queue.Reader.ReadAsync(cancellationToken);
        return workItem;
    }
}