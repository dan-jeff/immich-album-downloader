using System.ComponentModel.DataAnnotations;

namespace ImmichDownloader.Web.Models.Requests;

/// <summary>
/// Request model for image resize operations.
/// </summary>
public class ResizeRequest
{
    /// <summary>
    /// Gets or sets the unique identifier of the downloaded album to resize.
    /// </summary>
    [Required(ErrorMessage = "DownloadedAlbumId is required")]
    public int DownloadedAlbumId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the resize profile to use.
    /// </summary>
    [Required(ErrorMessage = "ProfileId is required")]
    public int ProfileId { get; set; }
}