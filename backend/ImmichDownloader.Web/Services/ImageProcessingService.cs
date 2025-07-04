using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using ImmichDownloader.Web.Models;
using System.IO.Compression;

namespace ImmichDownloader.Web.Services;

/// <summary>
/// Implementation of the image processing service using ImageSharp for resizing and batch processing.
/// This service handles individual image resizing with orientation correction, quality adjustments,
/// and batch processing with ZIP archive creation. Supports various image formats including HEIC/HEIF.
/// </summary>
public class ImageProcessingService : IImageProcessingService
{
    private readonly ILogger<ImageProcessingService> _logger;

    /// <summary>
    /// Initializes a new instance of the ImageProcessingService class.
    /// </summary>
    /// <param name="logger">The logger instance for this service.</param>
    public ImageProcessingService(ILogger<ImageProcessingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Resizes an image according to the specified profile settings while maintaining aspect ratio.
    /// Applies EXIF orientation correction and uses letterboxing with black background when needed.
    /// </summary>
    /// <param name="imageData">The raw image data to resize.</param>
    /// <param name="fileName">The name of the image file for logging purposes.</param>
    /// <param name="profile">The resize profile containing dimensions and orientation settings.</param>
    /// <returns>A tuple containing success status, processed image data, and any error message.</returns>
    public async Task<(bool Success, byte[]? ProcessedImage, string? Error)> ResizeImageAsync(
        byte[] imageData, 
        string fileName, 
        ResizeProfile profile)
    {
        try
        {
            using var image = Image.Load(imageData);
            
            // Apply EXIF orientation correction
            image.Mutate(ctx => ctx.AutoOrient());
            
            var originalWidth = image.Width;
            var originalHeight = image.Height;
            var isHorizontal = originalWidth >= originalHeight;

            // Skip if image orientation doesn't match profile settings
            if ((isHorizontal && !profile.IncludeHorizontal) || 
                (!isHorizontal && !profile.IncludeVertical))
            {
                return (false, null, "Image orientation doesn't match profile settings");
            }

            // Calculate aspect ratios for letterboxing
            var originalAspect = (double)originalWidth / originalHeight;
            var targetAspect = (double)profile.Width / profile.Height;

            int newWidth, newHeight;
            if (originalAspect > targetAspect)
            {
                // Image is wider - fit to target width
                newWidth = profile.Width;
                newHeight = (int)(newWidth / originalAspect);
            }
            else
            {
                // Image is taller - fit to target height
                newHeight = profile.Height;
                newWidth = (int)(newHeight * originalAspect);
            }

            // Create black canvas with target dimensions
            using var canvas = new Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(profile.Width, profile.Height);
            canvas.Mutate(ctx => ctx.BackgroundColor(Color.Black));

            // Resize image maintaining aspect ratio
            image.Mutate(ctx => ctx.Resize(newWidth, newHeight));

            // Center the resized image on the canvas
            var pasteX = (profile.Width - newWidth) / 2;
            var pasteY = (profile.Height - newHeight) / 2;

            canvas.Mutate(ctx => ctx.DrawImage(image, new Point(pasteX, pasteY), 1f));

            // Save as JPEG
            using var outputStream = new MemoryStream();
            await canvas.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = 90 });
            
            return (true, outputStream.ToArray(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing image {FileName}", fileName);
            return (false, null, $"Failed to resize {fileName}. Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes multiple images by resizing them according to the specified profile and packaging them into a ZIP archive.
    /// Supports progress reporting and cancellation. Images that fail processing are skipped and logged.
    /// </summary>
    /// <param name="images">Collection of image file names and their corresponding data.</param>
    /// <param name="profile">The resize profile to apply to all images.</param>
    /// <param name="progress">Optional progress reporter for tracking processing completion.</param>
    /// <param name="cancellationToken">Token to cancel the batch processing operation.</param>
    /// <returns>A tuple containing success status, ZIP archive data, and any error message.</returns>
    public async Task<(bool Success, byte[]? ZipData, string? Error)> ProcessImagesAsync(
        IEnumerable<(string FileName, byte[] Data)> images,
        ResizeProfile profile,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var zipStream = new MemoryStream();
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true);

            var imageList = images.ToList();
            var processedCount = 0;

            foreach (var (fileName, data) in imageList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (success, processedImage, error) = await ResizeImageAsync(data, fileName, profile);
                
                if (success && processedImage != null)
                {
                    var entry = archive.CreateEntry(fileName);
                    using var entryStream = entry.Open();
                    await entryStream.WriteAsync(processedImage, cancellationToken);
                }
                else if (!string.IsNullOrEmpty(error))
                {
                    _logger.LogWarning("Skipping image {FileName}: {Error}", fileName, error);
                }

                processedCount++;
                progress?.Report(processedCount);
            }

            archive.Dispose(); // Ensure archive is finalized before reading the stream
            return (true, zipStream.ToArray(), null);
        }
        catch (OperationCanceledException)
        {
            return (false, null, "Processing was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing images batch");
            return (false, null, $"Error processing images: {ex.Message}");
        }
    }
}