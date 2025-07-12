using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImmichDownloader.Web.Models;

[Table("resized_images")]
public class ResizedImage
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("image_id")]
    public int ImageId { get; set; }

    [Required]
    [Column("resize_job_id")]
    public int ResizeJobId { get; set; }

    [Required]
    [Column("file_path")]
    [StringLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [Column("file_size")]
    public long FileSize { get; set; }

    [Column("width")]
    public int Width { get; set; }

    [Column("height")]
    public int Height { get; set; }

    [Column("quality")]
    public int Quality { get; set; }

    [Column("format")]
    [StringLength(10)]
    public string Format { get; set; } = string.Empty;

    [Column("processing_time_ms")]
    public long? ProcessingTimeMs { get; set; }

    [Column("compression_ratio")]
    public decimal? CompressionRatio { get; set; }

    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = "pending"; // pending, processing, completed, error

    [Column("error_message")]
    [StringLength(500)]
    public string? ErrorMessage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [ForeignKey("ImageId")]
    public virtual Image Image { get; set; } = null!;

    [ForeignKey("ResizeJobId")]
    public virtual ResizeJob ResizeJob { get; set; } = null!;
}