using System.ComponentModel.DataAnnotations;

namespace ImmichDownloader.Web.Models.Requests;

/// <summary>
/// Configuration model for Immich server connection settings.
/// </summary>
public class ImmichConfiguration
{
    /// <summary>
    /// Gets or sets the URL of the Immich server.
    /// </summary>
    [Required(ErrorMessage = "Immich URL is required")]
    [Url(ErrorMessage = "Invalid URL format")]
    public string immich_url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key for Immich server authentication.
    /// </summary>
    [Required(ErrorMessage = "API key is required")]
    public string api_key { get; set; } = string.Empty;
}