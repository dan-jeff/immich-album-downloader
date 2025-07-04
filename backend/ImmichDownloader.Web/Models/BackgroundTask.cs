using System.ComponentModel.DataAnnotations;

namespace ImmichDownloader.Web.Models;

/// <summary>
/// Represents a background task in the system that can be either a download or resize operation.
/// Tracks the progress, status, and metadata for long-running operations.
/// </summary>
public class BackgroundTask
{
    /// <summary>
    /// Gets or sets the unique identifier for the background task.
    /// </summary>
    [Key]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Gets or sets the type of background task (Download or Resize).
    /// </summary>
    public TaskType TaskType { get; set; }
    
    /// <summary>
    /// Gets or sets the current status of the task.
    /// </summary>
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    
    /// <summary>
    /// Gets or sets the current progress count (e.g., number of images processed).
    /// </summary>
    public int Progress { get; set; }
    
    /// <summary>
    /// Gets or sets the total number of items to be processed.
    /// </summary>
    public int Total { get; set; }
    
    /// <summary>
    /// Gets or sets the current step description for the task.
    /// </summary>
    [MaxLength(500)]
    public string? CurrentStep { get; set; }
    
    /// <summary>
    /// Gets or sets the Immich album ID for download tasks.
    /// </summary>
    [MaxLength(36)]
    public string? AlbumId { get; set; }
    
    /// <summary>
    /// Gets or sets the album name for download tasks.
    /// </summary>
    [MaxLength(255)]
    public string? AlbumName { get; set; }
    
    /// <summary>
    /// Gets or sets the downloaded album ID for resize tasks.
    /// </summary>
    public int? DownloadedAlbumId { get; set; }
    
    /// <summary>
    /// Gets or sets the resize profile ID for resize tasks.
    /// </summary>
    public int? ProfileId { get; set; }
    
    /// <summary>
    /// Gets or sets the ZIP file data for legacy storage mode.
    /// </summary>
    public byte[]? ZipData { get; set; }
    
    /// <summary>
    /// Gets or sets the file path for streaming storage mode (alternative to ZipData).
    /// </summary>
    [MaxLength(500)]
    public string? FilePath { get; set; }
    
    /// <summary>
    /// Gets or sets the size of the ZIP data in bytes (legacy mode).
    /// </summary>
    public long ZipSize { get; set; }
    
    /// <summary>
    /// Gets or sets the file size in bytes when using streaming storage mode.
    /// </summary>
    public long FileSize { get; set; }
    
    /// <summary>
    /// Gets or sets the number of items that have been successfully processed.
    /// </summary>
    public int ProcessedCount { get; set; }
    
    /// <summary>
    /// Gets or sets the UTC timestamp when the task was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the UTC timestamp when the task was completed (null if not completed).
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}