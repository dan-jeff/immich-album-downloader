using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImmichDownloader.Web.Models;

/// <summary>
/// Represents a downloaded album from the Immich server stored in the local database.
/// Tracks metadata and storage information for albums that have been successfully downloaded.
/// Supports both legacy chunked storage and modern streaming file storage.
/// </summary>
public class DownloadedAlbum
{
    /// <summary>
    /// Gets or sets the unique identifier for the downloaded album.
    /// This is the primary key in the downloaded_albums table.
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets the original album identifier from the Immich server.
    /// This corresponds to the album ID in the Immich system and is used for tracking.
    /// Maximum length is 36 characters to accommodate UUID format.
    /// </summary>
    [Required]
    [MaxLength(36)]
    public string AlbumId { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the name of the album as it appears in Immich.
    /// Maximum length is 255 characters to accommodate various naming conventions.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string AlbumName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the total number of photos in the downloaded album.
    /// This count reflects the number of images that were successfully downloaded.
    /// </summary>
    public int PhotoCount { get; set; }
    
    /// <summary>
    /// Gets or sets the total size of the downloaded album in bytes.
    /// Includes all photos and any compression overhead from ZIP archiving.
    /// </summary>
    public long TotalSize { get; set; }
    
    /// <summary>
    /// Gets or sets the number of chunks used for legacy storage mode.
    /// In legacy mode, large albums are split into multiple chunks for database storage.
    /// Set to 0 for streaming storage mode.
    /// </summary>
    public int ChunkCount { get; set; }
    
    /// <summary>
    /// Gets or sets the UTC timestamp when the album was downloaded.
    /// Automatically set to the current UTC time when a new download is created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the file path for streaming downloads (alternative to chunks).
    /// When using streaming storage mode, this contains the path to the ZIP file on disk.
    /// Maximum length is 500 characters to accommodate various file system paths.
    /// Null when using legacy chunked storage mode.
    /// </summary>
    [MaxLength(500)]
    public string? FilePath { get; set; }
    
    /// <summary>
    /// Gets or sets the navigation property to the related Immich album information.
    /// This is a soft reference using AlbumId as the identifier, not a foreign key relationship.
    /// May be null if the album information is not cached locally.
    /// </summary>
    public ImmichAlbum? ImmichAlbum { get; set; }
    
    /// <summary>
    /// Gets or sets the collection of album chunks for legacy storage mode.
    /// Contains the actual ZIP file data split into manageable chunks for database storage.
    /// Empty collection when using streaming storage mode.
    /// </summary>
    public ICollection<AlbumChunk> Chunks { get; set; } = new List<AlbumChunk>();
}