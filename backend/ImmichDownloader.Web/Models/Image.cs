using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImmichDownloader.Web.Models;

[Table("images")]
public class Image
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("immich_id")]
    [StringLength(36)]
    public string ImmichId { get; set; } = string.Empty;

    [Required]
    [Column("original_filename")]
    [StringLength(255)]
    public string OriginalFilename { get; set; } = string.Empty;

    [Column("file_path")]
    [StringLength(500)]
    public string? FilePath { get; set; }

    [Column("file_size")]
    public long? FileSize { get; set; }

    [Required]
    [Column("file_type")]
    [StringLength(50)]
    public string FileType { get; set; } = string.Empty;

    [Column("width")]
    public int? Width { get; set; }

    [Column("height")]
    public int? Height { get; set; }

    [Column("checksum")]
    [StringLength(64)]
    public string? Checksum { get; set; }

    [Column("is_downloaded")]
    public bool IsDownloaded { get; set; } = false;

    [Column("download_attempts")]
    public int DownloadAttempts { get; set; } = 0;

    [Column("last_download_attempt")]
    public DateTime? LastDownloadAttempt { get; set; }

    [Column("downloaded_at")]
    public DateTime? DownloadedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<ImageAlbum> ImageAlbums { get; set; } = new List<ImageAlbum>();
    public virtual ICollection<ResizedImage> ResizedImages { get; set; } = new List<ResizedImage>();
}