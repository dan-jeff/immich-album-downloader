using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImmichDownloader.Web.Models;

/// <summary>
/// Represents a chunk of data from a downloaded album stored in the legacy database storage mode.
/// Large album ZIP files are split into smaller chunks for efficient database storage and retrieval.
/// This model is kept for backward compatibility; new downloads use streaming file storage.
/// </summary>
public class AlbumChunk
{
    /// <summary>
    /// Gets or sets the unique identifier for the album chunk.
    /// This is the primary key in the album_chunks table.
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets the original album identifier from the Immich server.
    /// This corresponds to the album ID in the Immich system.
    /// Maximum length is 36 characters to accommodate UUID format.
    /// </summary>
    [Required]
    [MaxLength(36)]
    public string AlbumId { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the foreign key reference to the parent downloaded album.
    /// This creates a relationship between the chunk and the album it belongs to.
    /// </summary>
    public int DownloadedAlbumId { get; set; }
    
    /// <summary>
    /// Gets or sets the zero-based index of this chunk within the complete album.
    /// Chunks are ordered sequentially and must be reassembled in the correct order.
    /// </summary>
    public int ChunkIndex { get; set; }
    
    /// <summary>
    /// Gets or sets the binary data for this chunk of the album ZIP file.
    /// Contains a portion of the complete ZIP file that must be combined with other chunks.
    /// </summary>
    [Required]
    public byte[] ChunkData { get; set; } = Array.Empty<byte>();
    
    /// <summary>
    /// Gets or sets the size of this chunk in bytes.
    /// Used for validation and progress tracking during chunk reassembly.
    /// </summary>
    public int ChunkSize { get; set; }
    
    /// <summary>
    /// Gets or sets the number of photos contained in this chunk.
    /// Used for progress tracking and validation during download processing.
    /// </summary>
    public int PhotoCount { get; set; }
    
    /// <summary>
    /// Gets or sets the UTC timestamp when the chunk was created.
    /// Automatically set to the current UTC time when a new chunk is stored.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the navigation property to the parent downloaded album.
    /// Provides access to the album that contains this chunk.
    /// </summary>
    [ForeignKey("DownloadedAlbumId")]
    public DownloadedAlbum? DownloadedAlbum { get; set; }
}