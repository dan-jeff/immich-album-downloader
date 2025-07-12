using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImmichDownloader.Web.Models;

[Table("albums")]
public class Album
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("immich_id")]
    [StringLength(36)]
    public string ImmichId { get; set; } = string.Empty;

    [Required]
    [Column("name")]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    [StringLength(1000)]
    public string? Description { get; set; }

    [Column("asset_count")]
    public int AssetCount { get; set; } = 0;

    [Column("is_shared")]
    public bool IsShared { get; set; } = false;

    [Column("owner_id")]
    [StringLength(36)]
    public string? OwnerId { get; set; }

    [Column("owner_name")]
    [StringLength(255)]
    public string? OwnerName { get; set; }

    [Column("thumbnail_asset_id")]
    [StringLength(36)]
    public string? ThumbnailAssetId { get; set; }

    [Column("last_synced")]
    public DateTime? LastSynced { get; set; }

    [Column("sync_status")]
    [StringLength(20)]
    public string SyncStatus { get; set; } = "pending"; // pending, syncing, completed, error

    [Column("sync_error")]
    [StringLength(1000)]
    public string? SyncError { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<ImageAlbum> ImageAlbums { get; set; } = new List<ImageAlbum>();
    public virtual ICollection<ResizeJob> ResizeJobs { get; set; } = new List<ResizeJob>();
}