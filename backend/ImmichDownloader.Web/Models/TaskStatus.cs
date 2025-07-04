namespace ImmichDownloader.Web.Models;

/// <summary>
/// Defines the possible states of a background task during its lifecycle.
/// </summary>
public enum TaskStatus
{
    /// <summary>
    /// Task has been created and is waiting to be processed.
    /// </summary>
    Pending,
    
    /// <summary>
    /// Task is currently being executed.
    /// </summary>
    InProgress,
    
    /// <summary>
    /// Task has completed successfully.
    /// </summary>
    Completed,
    
    /// <summary>
    /// Task encountered an error and could not be completed.
    /// </summary>
    Error
}