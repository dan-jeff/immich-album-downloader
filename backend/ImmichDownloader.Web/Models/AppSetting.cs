using System.ComponentModel.DataAnnotations;

namespace ImmichDownloader.Web.Models;

/// <summary>
/// Represents a configuration setting stored in the database.
/// Provides a flexible key-value store for application configuration that can be modified at runtime.
/// </summary>
public class AppSetting
{
    /// <summary>
    /// Gets or sets the unique identifier for the application setting.
    /// This is the primary key in the app_settings table.
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets the unique key name for the configuration setting.
    /// Maximum length is 100 characters and must be unique across all settings.
    /// Examples: "immich_url", "api_key", "max_download_size"
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the value for the configuration setting.
    /// Maximum length is 500 characters to accommodate various configuration values.
    /// Stored as string but can represent different data types depending on the key.
    /// </summary>
    [MaxLength(500)]
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the UTC timestamp when the setting was created.
    /// Automatically set to the current UTC time when a new setting is created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the UTC timestamp when the setting was last updated.
    /// Should be updated whenever the Value property is modified.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}