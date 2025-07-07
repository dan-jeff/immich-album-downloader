using System.ComponentModel.DataAnnotations;

namespace ImmichDownloader.Web.Models.Requests;

/// <summary>
/// Request model for creating or updating resize profiles.
/// </summary>
public class ResizeProfileRequest
{
    /// <summary>
    /// Gets or sets the name of the resize profile.
    /// </summary>
    [Required(ErrorMessage = "Name is required")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target width for resized images.
    /// </summary>
    [Range(1, 10000, ErrorMessage = "Width must be between 1 and 10000")]
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the target height for resized images.
    /// </summary>
    [Range(1, 10000, ErrorMessage = "Height must be between 1 and 10000")]
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets whether to include horizontal (landscape) images.
    /// </summary>
    public bool IncludeHorizontal { get; set; }

    /// <summary>
    /// Gets or sets whether to include vertical (portrait) images.
    /// </summary>
    public bool IncludeVertical { get; set; }

    /// <summary>
    /// Gets or sets the JPEG quality for resized images.
    /// Range from 1 (lowest quality, smallest file) to 100 (highest quality, largest file).
    /// Default is 85 for a good balance between quality and file size.
    /// </summary>
    [Range(1, 100, ErrorMessage = "Quality must be between 1 and 100")]
    public int Quality { get; set; } = 85;
}