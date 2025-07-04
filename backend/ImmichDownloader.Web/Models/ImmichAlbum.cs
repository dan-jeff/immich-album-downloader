using System.ComponentModel.DataAnnotations;

namespace ImmichDownloader.Web.Models;

/// <summary>
/// Represents an album from the Immich server cached locally for quick access.
/// Stores basic metadata about albums available on the connected Immich instance.
/// </summary>
public class ImmichAlbum
{
    /// <summary>
    /// Gets or sets the unique identifier for the album from the Immich server.
    /// This is the primary key and corresponds to the album ID in the Immich system.
    /// Maximum length is 36 characters to accommodate UUID format.
    /// </summary>
    [Key]
    [MaxLength(36)]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the name of the album as it appears in the Immich server.
    /// Maximum length is 255 characters to accommodate various naming conventions.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the total number of photos in the album.
    /// This count reflects the number of images available in the album on the Immich server.
    /// </summary>
    public int PhotoCount { get; set; }
    
    /// <summary>
    /// Gets or sets the UTC timestamp when the album information was last synchronized from the Immich server.
    /// Used to determine when the cached album data should be refreshed.
    /// </summary>
    public DateTime LastSynced { get; set; } = DateTime.UtcNow;
}