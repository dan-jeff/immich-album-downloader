using System.ComponentModel.DataAnnotations;

namespace ImmichDownloader.Web.Models;

/// <summary>
/// Represents a resize profile configuration for image processing operations.
/// Defines the target dimensions and orientation filters for resizing downloaded images.
/// </summary>
public class ResizeProfile
{
    /// <summary>
    /// Gets or sets the unique identifier for the resize profile.
    /// This is the primary key in the resize_profiles table.
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets the name of the resize profile.
    /// Must be unique and between 1-100 characters for easy identification.
    /// </summary>
    [Required(ErrorMessage = "Name is required")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    [MinLength(1, ErrorMessage = "Name cannot be empty")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the target width for resized images in pixels.
    /// Must be between 1 and 10000 pixels. Images will be resized to fit within this width.
    /// </summary>
    [Range(1, 10000, ErrorMessage = "Width must be between 1 and 10000 pixels")]
    public int Width { get; set; }
    
    /// <summary>
    /// Gets or sets the target height for resized images in pixels.
    /// Must be between 1 and 10000 pixels. Images will be resized to fit within this height.
    /// </summary>
    [Range(1, 10000, ErrorMessage = "Height must be between 1 and 10000 pixels")]
    public int Height { get; set; }
    
    /// <summary>
    /// Gets or sets whether to include horizontal (landscape) images in the resize operation.
    /// When false, landscape images will be excluded from processing.
    /// </summary>
    public bool IncludeHorizontal { get; set; }
    
    /// <summary>
    /// Gets or sets whether to include vertical (portrait) images in the resize operation.
    /// When false, portrait images will be excluded from processing.
    /// </summary>
    public bool IncludeVertical { get; set; }
    
    /// <summary>
    /// Gets or sets the UTC timestamp when the resize profile was created.
    /// Automatically set to the current UTC time when a new profile is created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}