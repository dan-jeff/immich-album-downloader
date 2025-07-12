using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImmichDownloader.Web.Models;

[Table("image_albums")]
public class ImageAlbum
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("image_id")]
    public int ImageId { get; set; }

    [Required]
    [Column("album_id")]
    public int AlbumId { get; set; }

    [Column("position_in_album")]
    public int? PositionInAlbum { get; set; }

    [Column("added_to_album_at")]
    public DateTime AddedToAlbumAt { get; set; } = DateTime.UtcNow;

    [Column("removed_from_album_at")]
    public DateTime? RemovedFromAlbumAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("ImageId")]
    public virtual Image Image { get; set; } = null!;

    [ForeignKey("AlbumId")]
    public virtual Album Album { get; set; } = null!;
}