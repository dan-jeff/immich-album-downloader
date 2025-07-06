using ImmichDownloader.Web.Data;

namespace ImmichDownloader.Web.Services.Database;

/// <summary>
/// Provides centralized database access with transaction management and error handling.
/// Abstracts database operations to improve consistency and reduce code duplication.
/// </summary>
public interface IDatabaseService
{
    /// <summary>
    /// Executes a database operation within a transaction scope.
    /// Automatically handles commit/rollback and provides consistent error handling.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The database operation to execute.</param>
    /// <returns>The result of the operation.</returns>
    Task<T> ExecuteInTransactionAsync<T>(Func<ApplicationDbContext, Task<T>> operation);

    /// <summary>
    /// Executes a database operation within a transaction scope without return value.
    /// Automatically handles commit/rollback and provides consistent error handling.
    /// </summary>
    /// <param name="operation">The database operation to execute.</param>
    Task ExecuteInTransactionAsync(Func<ApplicationDbContext, Task> operation);

    /// <summary>
    /// Executes a database operation within a service scope.
    /// Use this for operations that don't require explicit transaction management.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The database operation to execute.</param>
    /// <returns>The result of the operation.</returns>
    Task<T> ExecuteWithScopeAsync<T>(Func<ApplicationDbContext, Task<T>> operation);

    /// <summary>
    /// Executes a database operation within a service scope without return value.
    /// Use this for operations that don't require explicit transaction management.
    /// </summary>
    /// <param name="operation">The database operation to execute.</param>
    Task ExecuteWithScopeAsync(Func<ApplicationDbContext, Task> operation);

    /// <summary>
    /// Executes multiple database operations as a single atomic transaction.
    /// All operations succeed or all fail together.
    /// </summary>
    /// <param name="operations">The collection of database operations to execute.</param>
    Task ExecuteBatchTransactionAsync(params Func<ApplicationDbContext, Task>[] operations);

    /// <summary>
    /// Executes a database operation with retry logic for transient failures.
    /// Useful for operations that might fail due to temporary connectivity issues.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The database operation to execute.</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3).</param>
    /// <param name="delayMs">Delay between retries in milliseconds (default: 1000).</param>
    /// <returns>The result of the operation.</returns>
    Task<T> ExecuteWithRetryAsync<T>(Func<ApplicationDbContext, Task<T>> operation, int maxRetries = 3, int delayMs = 1000);
}