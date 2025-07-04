namespace ImmichDownloader.Web.Models;

/// <summary>
/// Defines the types of background tasks that can be executed in the system.
/// </summary>
public enum TaskType
{
    /// <summary>
    /// Download task for fetching images from Immich server and creating ZIP archives.
    /// </summary>
    Download,
    
    /// <summary>
    /// Resize task for processing downloaded images according to specified profiles.
    /// </summary>
    Resize
}