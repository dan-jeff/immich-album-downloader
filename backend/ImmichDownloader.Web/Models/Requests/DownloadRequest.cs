using System.ComponentModel.DataAnnotations;

namespace ImmichDownloader.Web.Models.Requests;

/// <summary>
/// Request model for album download operations.
/// </summary>
public class DownloadRequest
{
    /// <summary>
    /// Gets or sets the unique identifier of the album to download.
    /// </summary>
    [Required(ErrorMessage = "AlbumId is required")]
    public string AlbumId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the album to download.
    /// </summary>
    [Required(ErrorMessage = "AlbumName is required")]
    public string AlbumName { get; set; } = string.Empty;
}