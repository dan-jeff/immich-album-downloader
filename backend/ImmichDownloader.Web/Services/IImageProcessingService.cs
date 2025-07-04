using ImmichDownloader.Web.Models;

namespace ImmichDownloader.Web.Services;

/// <summary>
/// Service interface for image processing operations including resizing and batch processing.
/// Provides methods for resizing individual images and processing multiple images with progress tracking.
/// </summary>
public interface IImageProcessingService
{
    /// <summary>
    /// Resizes a single image according to the specified resize profile.
    /// Supports various image formats and applies the resize settings including dimensions,
    /// quality, and orientation-based filtering.
    /// </summary>
    /// <param name="imageData">The binary data of the image to resize.</param>
    /// <param name="fileName">The filename of the image (used for format detection and logging).</param>
    /// <param name="profile">The resize profile containing the target dimensions and processing settings.</param>
    /// <returns>
    /// A task that represents the asynchronous resize operation. The task result contains:
    /// - Success: true if the image was resized successfully, false otherwise
    /// - ProcessedImage: the resized image data if successful, null otherwise
    /// - Error: error message if the operation failed, null otherwise
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when imageData, fileName, or profile is null.</exception>
    /// <exception cref="ArgumentException">Thrown when imageData is empty or fileName is empty.</exception>
    /// <exception cref="NotSupportedException">Thrown when the image format is not supported.</exception>
    Task<(bool Success, byte[]? ProcessedImage, string? Error)> ResizeImageAsync(
        byte[] imageData, 
        string fileName, 
        ResizeProfile profile);
        
    /// <summary>
    /// Processes multiple images by resizing them according to the specified profile and packaging them into a ZIP archive.
    /// Supports progress tracking and cancellation for long-running operations.
    /// </summary>
    /// <param name="images">A collection of image data with filenames to process.</param>
    /// <param name="profile">The resize profile containing the target dimensions and processing settings.</param>
    /// <param name="progress">Optional progress reporter for tracking processing progress.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous batch processing operation. The task result contains:
    /// - Success: true if all images were processed successfully, false otherwise
    /// - ZipData: the ZIP archive containing processed images if successful, null otherwise
    /// - Error: error message if the operation failed, null otherwise
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when images or profile is null.</exception>
    /// <exception cref="ArgumentException">Thrown when images collection is empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    Task<(bool Success, byte[]? ZipData, string? Error)> ProcessImagesAsync(
        IEnumerable<(string FileName, byte[] Data)> images,
        ResizeProfile profile,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}