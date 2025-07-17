using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImmichDownloader.Web.Models;

/// <summary>
/// Represents an individual asset (photo/video) that has been downloaded from an Immich album.
/// Tracks metadata and relationships for individual files within downloaded albums.
/// </summary>
public class DownloadedAsset
{
    /// <summary>
    /// Gets or sets the unique identifier for the downloaded asset.
    /// This is the primary key in the downloaded_assets table.
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets the original asset identifier from the Immich server.
    /// This corresponds to the asset ID in the Immich system and is used for tracking.
    /// Maximum length is 36 characters to accommodate UUID format.
    /// </summary>
    [Required]
    [MaxLength(36)]
    public string AssetId { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the album identifier that this asset belongs to.
    /// This corresponds to the album ID in the Immich system.
    /// Maximum length is 36 characters to accommodate UUID format.
    /// </summary>
    [Required]
    [MaxLength(36)]
    public string AlbumId { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the original file name of the asset as it appears in Immich.
    /// Maximum length is 255 characters to accommodate various naming conventions.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the file size of the downloaded asset in bytes.
    /// Represents the actual size of the image or video file.
    /// </summary>
    public long FileSize { get; set; }
    
    /// <summary>
    /// Gets or sets the UTC timestamp when the asset was downloaded.
    /// Automatically set to the current UTC time when a new asset is downloaded.
    /// </summary>
    public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the navigation property to the parent downloaded album.
    /// Provides access to the album that contains this asset.
    /// </summary>
    public DownloadedAlbum? DownloadedAlbum { get; set; }
    
    /// <summary>
    /// Gets or sets the foreign key reference to the parent downloaded album.
    /// This creates a relationship between the asset and the album it belongs to.
    /// Can be null when assets are recorded before the album is fully saved.
    /// </summary>
    public int? DownloadedAlbumId { get; set; }
}