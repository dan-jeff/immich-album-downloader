using ImmichDownloader.Web.Models;
using ImmichDownloader.Web.Services.Configuration;
using ImmichDownloader.Web.Services.Repositories;
using System.Diagnostics;

namespace ImmichDownloader.Web.Services.Sync;

public class AlbumSyncService : IAlbumSyncService
{
    private readonly IAlbumRepository _albumRepository;
    private readonly IImageRepository _imageRepository;
    private readonly IImageAlbumRepository _imageAlbumRepository;
    private readonly IImmichConfigurationService _configurationService;
    private readonly IImmichService _immichService;
    private readonly ILogger<AlbumSyncService> _logger;

    public AlbumSyncService(
        IAlbumRepository albumRepository,
        IImageRepository imageRepository,
        IImageAlbumRepository imageAlbumRepository,
        IImmichConfigurationService configurationService,
        IImmichService immichService,
        ILogger<AlbumSyncService> logger)
    {
        _albumRepository = albumRepository;
        _imageRepository = imageRepository;
        _imageAlbumRepository = imageAlbumRepository;
        _configurationService = configurationService;
        _immichService = immichService;
        _logger = logger;
    }

    public async Task<AlbumSyncResult> SyncAlbumAsync(string immichAlbumId)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new AlbumSyncResult();

        try
        {
            _logger.LogInformation("Starting sync for album {AlbumId}", immichAlbumId);

            // Configure Immich service
            var (url, apiKey) = await _configurationService.GetImmichSettingsAsync();
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(apiKey))
            {
                result.ErrorMessage = "Immich configuration not found";
                _logger.LogError("Immich configuration not found");
                return result;
            }

            _immichService.Configure(url, apiKey);

            // Fetch album details from Immich
            var (albumSuccess, immichAlbum, albumError) = await _immichService.GetAlbumInfoAsync(immichAlbumId);
            if (!albumSuccess || immichAlbum == null)
            {
                result.ErrorMessage = albumError ?? $"Album {immichAlbumId} not found in Immich";
                _logger.LogError("Album {AlbumId} not found in Immich: {Error}", immichAlbumId, albumError);
                return result;
            }

            // Create album metadata
            var albumMetadata = new Album
            {
                ImmichId = immichAlbumId,
                Name = immichAlbum.AlbumName,
                AssetCount = immichAlbum.Assets?.Count() ?? 0,
                // Additional properties would be populated from Immich API if available
            };

            var assetIds = immichAlbum.Assets?.Select(a => a.Id) ?? Enumerable.Empty<string>();
            
            result = await SyncAlbumFromMetadataAsync(albumMetadata, assetIds);
            
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("Completed sync for album {AlbumId} in {Duration}ms. " +
                                 "New: {New}, Existing: {Existing}, Removed: {Removed}",
                immichAlbumId, stopwatch.ElapsedMilliseconds, 
                result.NewAssets, result.ExistingAssets, result.RemovedAssets);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.ErrorMessage = ex.Message;
            
            _logger.LogError(ex, "Error syncing album {AlbumId}", immichAlbumId);
            
            // Update album sync status to error
            var existingAlbum = await _albumRepository.GetByImmichIdAsync(immichAlbumId);
            if (existingAlbum != null)
            {
                await _albumRepository.UpdateSyncStatusAsync(existingAlbum.Id, "error", ex.Message);
            }

            return result;
        }
    }

    public async Task<AlbumSyncResult> SyncAlbumFromMetadataAsync(Album albumMetadata, IEnumerable<string> assetIds)
    {
        var result = new AlbumSyncResult { Album = albumMetadata };

        try
        {
            var assetIdsList = assetIds.ToList();
            result.TotalAssets = assetIdsList.Count;

            // Get or create album
            var existingAlbum = await _albumRepository.GetByImmichIdAsync(albumMetadata.ImmichId);
            Album album;

            if (existingAlbum == null)
            {
                // Create new album
                albumMetadata.SyncStatus = "syncing";
                album = await _albumRepository.CreateAsync(albumMetadata);
                _logger.LogDebug("Created new album: {AlbumName} ({AlbumId})", album.Name, album.ImmichId);
            }
            else
            {
                // Update existing album
                existingAlbum.Name = albumMetadata.Name;
                existingAlbum.AssetCount = albumMetadata.AssetCount;
                existingAlbum.SyncStatus = "syncing";
                existingAlbum.SyncError = null;
                album = await _albumRepository.UpdateAsync(existingAlbum);
                _logger.LogDebug("Updated existing album: {AlbumName} ({AlbumId})", album.Name, album.ImmichId);
            }

            result.Album = album;

            // Process images and relationships
            await ProcessAlbumImagesAsync(album, assetIdsList, result);

            // Update album sync status
            await _albumRepository.UpdateSyncStatusAsync(album.Id, "completed");
            result.Success = true;

            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Error syncing album metadata for {AlbumId}", albumMetadata.ImmichId);
            throw;
        }
    }

    private async Task ProcessAlbumImagesAsync(Album album, List<string> assetIds, AlbumSyncResult result)
    {
        // Get existing images and relationships
        var existingImages = await _imageRepository.GetByImmichIdsAsync(assetIds);
        var existingImageMap = existingImages.ToDictionary(i => i.ImmichId, i => i);
        
        var existingRelationships = await _imageAlbumRepository.GetByAlbumIdAsync(album.Id, activeOnly: false);
        var existingRelationshipMap = existingRelationships.ToDictionary(r => r.Image?.ImmichId ?? "", r => r);

        // Process each asset
        foreach (var assetId in assetIds)
        {
            try
            {
                await ProcessSingleImageAsync(album, assetId, existingImageMap, existingRelationshipMap, result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing asset {AssetId} for album {AlbumId}", assetId, album.ImmichId);
            }
        }

        // Deactivate relationships for images no longer in the album
        await DeactivateRemovedImagesAsync(album, assetIds, existingRelationships, result);
    }

    private async Task ProcessSingleImageAsync(
        Album album, 
        string assetId, 
        Dictionary<string, Image> existingImageMap,
        Dictionary<string, ImageAlbum> existingRelationshipMap,
        AlbumSyncResult result)
    {
        Image image;

        // Get or create image
        if (existingImageMap.TryGetValue(assetId, out var existingImage))
        {
            image = existingImage;
            result.ExistingAssets++;
        }
        else
        {
            // Create new image record (will be downloaded later)
            image = new Image
            {
                ImmichId = assetId,
                OriginalFilename = $"{assetId}.jpg", // Placeholder, will be updated during download
                FileType = "image", // Placeholder
                IsDownloaded = false
            };
            
            image = await _imageRepository.CreateAsync(image);
            result.NewAssets++;
            _logger.LogDebug("Created new image record for asset {AssetId}", assetId);
        }

        // Get or create image-album relationship
        if (existingRelationshipMap.TryGetValue(assetId, out var existingRelationship))
        {
            // Reactivate if needed
            if (!existingRelationship.IsActive)
            {
                await _imageAlbumRepository.ActivateAsync(image.Id, album.Id);
                _logger.LogDebug("Reactivated image-album relationship for asset {AssetId}", assetId);
            }
        }
        else
        {
            // Create new relationship
            var imageAlbum = new ImageAlbum
            {
                ImageId = image.Id,
                AlbumId = album.Id,
                IsActive = true
            };
            
            await _imageAlbumRepository.CreateAsync(imageAlbum);
            _logger.LogDebug("Created new image-album relationship for asset {AssetId}", assetId);
        }
    }

    private async Task DeactivateRemovedImagesAsync(
        Album album, 
        List<string> currentAssetIds, 
        IEnumerable<ImageAlbum> existingRelationships,
        AlbumSyncResult result)
    {
        var currentAssetSet = currentAssetIds.ToHashSet();
        
        foreach (var relationship in existingRelationships.Where(r => r.IsActive))
        {
            if (relationship.Image != null && !currentAssetSet.Contains(relationship.Image.ImmichId))
            {
                await _imageAlbumRepository.DeactivateAsync(relationship.ImageId, relationship.AlbumId);
                result.RemovedAssets++;
                _logger.LogDebug("Deactivated image-album relationship for removed asset {AssetId}", 
                    relationship.Image.ImmichId);
            }
        }
    }

    public async Task<IEnumerable<Album>> GetAlbumsNeedingSyncAsync()
    {
        try
        {
            return await _albumRepository.GetAlbumsNeedingSyncAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving albums needing sync");
            return Enumerable.Empty<Album>();
        }
    }

    public async Task<SyncSummary> SyncAllAlbumsAsync()
    {
        var summary = new SyncSummary();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting sync for all albums");

            // Check configuration
            if (!await _configurationService.IsConfiguredAsync())
            {
                var error = "Immich configuration not found";
                summary.Errors.Add(error);
                _logger.LogError(error);
                return summary;
            }

            // Get all albums from Immich
            var (url, apiKey) = await _configurationService.GetImmichSettingsAsync();
            _immichService.Configure(url!, apiKey!);
            
            var (albumsSuccess, immichAlbums, albumsError) = await _immichService.GetAlbumsAsync();
            if (!albumsSuccess || immichAlbums == null)
            {
                var error = albumsError ?? "Failed to retrieve albums from Immich";
                summary.Errors.Add(error);
                _logger.LogError(error);
                return summary;
            }

            summary.TotalAlbums = immichAlbums.Count();

            if (!immichAlbums.Any())
            {
                _logger.LogWarning("No albums found in Immich");
                return summary;
            }

            // Sync each album
            foreach (var immichAlbum in immichAlbums)
            {
                try
                {
                    var result = await SyncAlbumAsync(immichAlbum.Id);
                    
                    if (result.Success)
                    {
                        summary.SuccessfulSyncs++;
                        summary.TotalNewAssets += result.NewAssets;
                        summary.TotalRemovedAssets += result.RemovedAssets;
                    }
                    else
                    {
                        summary.FailedSyncs++;
                        if (!string.IsNullOrEmpty(result.ErrorMessage))
                        {
                            summary.Errors.Add($"{immichAlbum.AlbumName}: {result.ErrorMessage}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    summary.FailedSyncs++;
                    summary.Errors.Add($"{immichAlbum.AlbumName}: {ex.Message}");
                    _logger.LogError(ex, "Error syncing album {AlbumName}", immichAlbum.AlbumName);
                }
            }

            stopwatch.Stop();
            summary.TotalDuration = stopwatch.Elapsed;

            _logger.LogInformation("Completed sync for all albums in {Duration}ms. " +
                                 "Success: {Success}, Failed: {Failed}, New Assets: {NewAssets}",
                stopwatch.ElapsedMilliseconds, summary.SuccessfulSyncs, summary.FailedSyncs, 
                summary.TotalNewAssets);

            return summary;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            summary.TotalDuration = stopwatch.Elapsed;
            summary.Errors.Add($"General sync error: {ex.Message}");
            
            _logger.LogError(ex, "Error during full album sync");
            return summary;
        }
    }

    public async Task<bool> IsAlbumSyncedAsync(string immichAlbumId)
    {
        try
        {
            var album = await _albumRepository.GetByImmichIdAsync(immichAlbumId);
            return album != null && album.SyncStatus == "completed" && album.LastSynced.HasValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking sync status for album {AlbumId}", immichAlbumId);
            return false;
        }
    }

    public async Task<DateTime?> GetLastSyncTimeAsync(string immichAlbumId)
    {
        try
        {
            var album = await _albumRepository.GetByImmichIdAsync(immichAlbumId);
            return album?.LastSynced;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving last sync time for album {AlbumId}", immichAlbumId);
            return null;
        }
    }
}