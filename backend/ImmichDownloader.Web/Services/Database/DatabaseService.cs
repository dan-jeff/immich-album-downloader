using ImmichDownloader.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace ImmichDownloader.Web.Services.Database;

/// <summary>
/// Implementation of centralized database service with transaction management and error handling.
/// Provides consistent database access patterns and reduces code duplication across the application.
/// </summary>
public class DatabaseService : IDatabaseService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(IServiceScopeFactory scopeFactory, ILogger<DatabaseService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<T> ExecuteInTransactionAsync<T>(Func<ApplicationDbContext, Task<T>> operation)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            _logger.LogDebug("Starting database transaction");
            var result = await operation(context);
            await transaction.CommitAsync();
            _logger.LogDebug("Database transaction committed successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database transaction failed, rolling back");
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(Func<ApplicationDbContext, Task> operation)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            _logger.LogDebug("Starting database transaction");
            await operation(context);
            await transaction.CommitAsync();
            _logger.LogDebug("Database transaction committed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database transaction failed, rolling back");
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T> ExecuteWithScopeAsync<T>(Func<ApplicationDbContext, Task<T>> operation)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            return await operation(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database operation failed");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ExecuteWithScopeAsync(Func<ApplicationDbContext, Task> operation)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            await operation(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database operation failed");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ExecuteBatchTransactionAsync(params Func<ApplicationDbContext, Task>[] operations)
    {
        if (operations == null || operations.Length == 0)
        {
            _logger.LogWarning("No operations provided for batch transaction");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            _logger.LogDebug("Starting batch database transaction with {OperationCount} operations", operations.Length);
            
            foreach (var operation in operations)
            {
                await operation(context);
            }
            
            await transaction.CommitAsync();
            _logger.LogDebug("Batch database transaction committed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch database transaction failed, rolling back");
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T> ExecuteWithRetryAsync<T>(Func<ApplicationDbContext, Task<T>> operation, int maxRetries = 3, int delayMs = 1000)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= maxRetries)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return await operation(context);
            }
            catch (Exception ex) when (IsTransientException(ex) && attempt < maxRetries)
            {
                lastException = ex;
                attempt++;
                var delay = delayMs * attempt; // Exponential backoff
                
                _logger.LogWarning(ex, "Database operation failed (attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms", 
                    attempt, maxRetries + 1, delay);
                
                await Task.Delay(delay);
            }
        }

        _logger.LogError(lastException, "Database operation failed after {MaxRetries} retries", maxRetries);
        throw lastException ?? new InvalidOperationException("Operation failed without exception details");
    }

    /// <summary>
    /// Determines if an exception is transient and worth retrying.
    /// </summary>
    /// <param name="exception">The exception to check.</param>
    /// <returns>True if the exception is transient, false otherwise.</returns>
    private static bool IsTransientException(Exception exception)
    {
        return exception switch
        {
            // Entity Framework transient exceptions
            DbUpdateException => true,
            InvalidOperationException ex when ex.Message.Contains("timeout") => true,
            TimeoutException => true,
            
            // SQLite specific transient errors
            Microsoft.Data.Sqlite.SqliteException ex => ex.SqliteErrorCode switch
            {
                5 => true,  // SQLITE_BUSY
                6 => true,  // SQLITE_LOCKED
                _ => false
            },
            
            // Network/IO related errors
            IOException => true,
            System.Net.Sockets.SocketException => true,
            
            _ => false
        };
    }
}