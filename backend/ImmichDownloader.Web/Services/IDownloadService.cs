namespace ImmichDownloader.Web.Services;

/// <summary>
/// Service interface for downloading albums from the Immich server.
/// Provides methods for asynchronous album downloads with progress tracking and cancellation support.
/// </summary>
public interface IDownloadService
{
    /// <summary>
    /// Downloads an album from the Immich server asynchronously.
    /// The download process includes fetching album information, downloading all assets,
    /// and reporting progress through the background task system.
    /// </summary>
    /// <param name="taskId">The unique identifier of the background task associated with this download.</param>
    /// <param name="albumId">The unique identifier of the album to download from the Immich server.</param>
    /// <param name="albumName">The name of the album being downloaded (used for logging and progress reporting).</param>
    /// <param name="cancellationToken">A cancellation token to cancel the download operation.</param>
    /// <returns>A task that represents the asynchronous download operation.</returns>
    /// <exception cref="ArgumentException">Thrown when taskId, albumId, or albumName is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the service dependencies are not properly configured.</exception>
    /// <exception cref="HttpRequestException">Thrown when communication with the Immich server fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    Task DownloadAlbumAsync(string taskId, string albumId, string albumName, CancellationToken cancellationToken);
}