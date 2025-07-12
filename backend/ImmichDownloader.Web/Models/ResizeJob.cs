using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImmichDownloader.Web.Models;

[Table("resize_jobs")]
public class ResizeJob
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("job_id")]
    [StringLength(36)]
    public string JobId { get; set; } = Guid.NewGuid().ToString();

    [Column("album_id")]
    public int? AlbumId { get; set; }

    [Required]
    [Column("resize_profile_id")]
    public int ResizeProfileId { get; set; }

    [Required]
    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = "pending"; // pending, processing, completed, error, cancelled

    [Column("total_images")]
    public int TotalImages { get; set; } = 0;

    [Column("processed_images")]
    public int ProcessedImages { get; set; } = 0;

    [Column("skipped_images")]
    public int SkippedImages { get; set; } = 0;

    [Column("failed_images")]
    public int FailedImages { get; set; } = 0;

    [Column("output_zip_path")]
    [StringLength(500)]
    public string? OutputZipPath { get; set; }

    [Column("output_zip_size")]
    public long? OutputZipSize { get; set; }

    [Column("error_message")]
    [StringLength(1000)]
    public string? ErrorMessage { get; set; }

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("AlbumId")]
    public virtual Album? Album { get; set; }

    [ForeignKey("ResizeProfileId")]
    public virtual ResizeProfile ResizeProfile { get; set; } = null!;

    public virtual ICollection<ResizedImage> ResizedImages { get; set; } = new List<ResizedImage>();
}