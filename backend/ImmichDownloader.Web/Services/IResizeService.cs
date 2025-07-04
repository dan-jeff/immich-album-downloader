namespace ImmichDownloader.Web.Services;

/// <summary>
/// Service interface for resizing downloaded albums according to specified resize profiles.
/// Provides methods for processing entire albums with background task support and progress tracking.
/// </summary>
public interface IResizeService
{
    /// <summary>
    /// Resizes all images in a downloaded album according to the specified resize profile.
    /// This operation processes the album's ZIP chunks, extracts images, resizes them,
    /// and creates new ZIP archives with the resized images.
    /// </summary>
    /// <param name="taskId">The unique identifier of the background task associated with this resize operation.</param>
    /// <param name="downloadedAlbumId">The database ID of the downloaded album to resize.</param>
    /// <param name="profileId">The database ID of the resize profile to apply.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the resize operation.</param>
    /// <returns>A task that represents the asynchronous resize operation.</returns>
    /// <exception cref="ArgumentException">Thrown when taskId is null or empty, or when downloadedAlbumId or profileId is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the album or profile is not found, or when the service dependencies are not properly configured.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    Task ResizeAlbumAsync(string taskId, int downloadedAlbumId, int profileId, CancellationToken cancellationToken);
}