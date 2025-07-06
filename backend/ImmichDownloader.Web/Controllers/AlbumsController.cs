using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using ImmichDownloader.Web.Services;
using Immich.Data.Models;

namespace ImmichDownloader.Web.Controllers;

/// <summary>
/// Controller responsible for managing albums including fetching from Immich server,
/// synchronizing with local database, and providing thumbnails through proxy.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class AlbumsController : SecureControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IImmichService _immichService;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlbumsController"/> class.
    /// </summary>
    /// <param name="context">The database context for album operations.</param>
    /// <param name="immichService">The service for communicating with Immich server.</param>
    /// <param name="configuration">The application configuration provider.</param>
    /// <param name="httpClientFactory">Factory for creating HTTP clients.</param>
    /// <param name="logger">Logger instance for logging operations and errors.</param>
    public AlbumsController(
        ApplicationDbContext context,
        IImmichService immichService,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<AlbumsController> logger) : base(logger)
    {
        _context = context;
        _immichService = immichService;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Retrieves all albums from the Immich server and synchronizes them with the local database.
    /// Includes both remote asset counts and local download counts.
    /// </summary>
    /// <returns>A list of albums with their metadata and asset counts.</returns>
    /// <response code="200">Returns the list of albums.</response>
    /// <response code="400">Immich configuration not set.</response>
    /// <response code="401">Unauthorized access.</response>
    /// <response code="500">Error communicating with Immich server.</response>
    [HttpGet("albums")]
    public async Task<IActionResult> GetAlbums()
    {
        var immichUrl = await GetSettingAsync("Immich:Url");
        var apiKey = await GetSettingAsync("Immich:ApiKey");
        
        Logger.LogInformation("Fetching albums for user {Username} - URL: '{Url}', API Key set: {ApiKeySet}", 
            GetCurrentUsername(), immichUrl, !string.IsNullOrEmpty(apiKey));

        if (string.IsNullOrEmpty(immichUrl) || string.IsNullOrEmpty(apiKey))
        {
            Logger.LogWarning("Configuration not set for user {Username}", GetCurrentUsername());
            return CreateErrorResponse(400, "Configuration not set");
        }

        _immichService.Configure(immichUrl, apiKey);

        // Test connection first to handle container restart scenarios
        var (connectionSuccess, connectionMessage) = await _immichService.ValidateConnectionAsync(immichUrl, apiKey);
        if (!connectionSuccess)
        {
            Logger.LogWarning("Immich connection validation failed for user {Username}: {Message}", GetCurrentUsername(), connectionMessage);
            return CreateErrorResponse(500, $"Connection failed: {connectionMessage}");
        }

        var (success, albums, error) = await _immichService.GetAlbumsAsync();
        if (!success)
        {
            Logger.LogError("Failed to get albums for user {Username}: {Error}", GetCurrentUsername(), error);
            return CreateErrorResponse(500, error ?? "Failed to retrieve albums");
        }

        // Debug logging to see what ImmichService returned
        Logger.LogInformation("=== Albums returned from ImmichService for user {Username} ===", GetCurrentUsername());
        foreach (var album in albums ?? new List<AlbumModel>())
        {
            Logger.LogInformation("Album: {AlbumName}, AssetCount: {AssetCount}, AlbumThumbnailAssetId: '{AlbumThumbnailAssetId}'", 
                album.AlbumName, album.AssetCount, album.AlbumThumbnailAssetId ?? "null");
        }
        Logger.LogInformation("=== End Albums from ImmichService for user {Username} ===", GetCurrentUsername());

        // Sync albums to database
        await SyncAlbumsToDatabase(albums!);

        // Get local download counts
        var localCounts = await GetLocalAssetCounts();

        // Transform to match frontend expectations - with real data
        var result = albums!.Select(album => new
        {
            id = album.Id,
            albumName = album.AlbumName,
            description = "",
            albumThumbnailAssetId = album.AlbumThumbnailAssetId,
            createdAt = DateTime.UtcNow,
            updatedAt = DateTime.UtcNow,
            ownerId = "",
            owner = new { },
            albumUsers = new List<object>(),
            shared = false,
            hasSharedLink = false,
            startDate = DateTime.UtcNow,
            endDate = DateTime.UtcNow,
            assets = new List<object>(),
            assetCount = album.AssetCount,
            isActivityEnabled = false,
            order = new { },
            lastModifiedAssetTimestamp = DateTime.UtcNow,
            localAssetCount = localCounts.GetValueOrDefault(album.Id, 0)
        });

        return Ok(result);
    }

    /// <summary>
    /// Retrieves all albums that have been downloaded to the local system.
    /// </summary>
    /// <returns>A list of downloaded albums with their metadata.</returns>
    /// <response code="200">Returns the list of downloaded albums.</response>
    /// <response code="401">Unauthorized access.</response>
    [HttpGet("downloaded-albums")]
    public async Task<IActionResult> GetDownloadedAlbums()
    {
        var downloaded = await _context.DownloadedAlbums
            .Include(d => d.ImmichAlbum)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new
            {
                id = d.Id,
                albumName = d.AlbumName,
                assetCount = 0,
                localAssetCount = d.PhotoCount,
                shared = false,
                sharedUsers = new List<object>()
            })
            .ToListAsync();

        return Ok(downloaded);
    }

    /// <summary>
    /// Retrieves statistics about albums, images, and downloads.
    /// Attempts to refresh data from Immich server if configured.
    /// </summary>
    /// <returns>Statistics including album count, image count, and download count.</returns>
    /// <response code="200">Returns the statistics.</response>
    /// <response code="401">Unauthorized access.</response>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var albumCount = await _context.ImmichAlbums.CountAsync();
        var totalPhotos = await _context.ImmichAlbums.SumAsync(a => a.PhotoCount);
        var downloadCount = await _context.DownloadedAlbums.CountAsync();

        var stats = new
        {
            album_count = albumCount,
            image_count = totalPhotos,
            download_count = downloadCount
        };

        // Try to update from Immich if configured
        var immichUrl = await GetSettingAsync("Immich:Url");
        var apiKey = await GetSettingAsync("Immich:ApiKey");

        if (!string.IsNullOrEmpty(immichUrl) && !string.IsNullOrEmpty(apiKey))
        {
            try
            {
                _immichService.Configure(immichUrl, apiKey);
                var (success, albums, _) = await _immichService.GetAlbumsAsync();
                if (success && albums != null)
                {
                    await SyncAlbumsToDatabase(albums);
                    // Recalculate stats after sync
                    albumCount = await _context.ImmichAlbums.CountAsync();
                    totalPhotos = await _context.ImmichAlbums.SumAsync(a => a.PhotoCount);
                    
                    return Ok(new
                    {
                        album_count = albumCount,
                        image_count = totalPhotos,
                        download_count = downloadCount
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting live stats for user {Username}, returning cached data", GetCurrentUsername());
                // Return cached stats on error
            }
        }

        return Ok(stats);
    }

    /// <summary>
    /// Proxies thumbnail requests to the Immich server, adding authentication headers.
    /// Caches thumbnails for 24 hours to improve performance.
    /// </summary>
    /// <param name="assetId">The ID of the asset to retrieve the thumbnail for.</param>
    /// <returns>The thumbnail image data.</returns>
    /// <response code="200">Returns the thumbnail image.</response>
    /// <response code="400">Immich configuration not set.</response>
    /// <response code="401">Unauthorized access.</response>
    /// <response code="404">Thumbnail not found.</response>
    [HttpGet("proxy/thumbnail/{assetId}")]
    public async Task<IActionResult> ProxyThumbnail(string assetId)
    {
        // Validate asset ID to prevent injection attacks
        var sanitizedAssetId = ValidateAndSanitizeFileName(assetId, "assetId");
        if (sanitizedAssetId == null)
        {
            Logger.LogWarning("Invalid asset ID '{AssetId}' provided by user {Username}", assetId, GetCurrentUsername());
            return CreateErrorResponse(400, "Invalid asset ID");
        }

        var immichUrl = await GetSettingAsync("Immich:Url");
        var apiKey = await GetSettingAsync("Immich:ApiKey");

        if (string.IsNullOrEmpty(immichUrl) || string.IsNullOrEmpty(apiKey))
        {
            Logger.LogWarning("Configuration not set for thumbnail request by user {Username}", GetCurrentUsername());
            return CreateErrorResponse(400, "Configuration not set");
        }

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

            var baseUrl = immichUrl.EndsWith('/') ? immichUrl : immichUrl + "/";
            var thumbnailUrl = $"{baseUrl}api/assets/{sanitizedAssetId}/thumbnail?size=preview";

            Logger.LogInformation("Proxying thumbnail for asset {AssetId} for user {Username}", sanitizedAssetId, GetCurrentUsername());
            var response = await httpClient.GetAsync(thumbnailUrl);
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("Thumbnail not found for asset {AssetId}, user {Username}", sanitizedAssetId, GetCurrentUsername());
                return CreateErrorResponse(404, "Thumbnail not found");
            }

            var content = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";

            Response.Headers.Append("Cache-Control", "max-age=86400"); // Cache for 1 day
            return File(content, contentType);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error proxying thumbnail for asset {AssetId}, user {Username}", sanitizedAssetId, GetCurrentUsername());
            return CreateErrorResponse(404, "Thumbnail not found");
        }
    }

    /// <summary>
    /// Synchronizes album data from Immich server to the local database.
    /// Updates existing albums or creates new ones as needed.
    /// </summary>
    /// <param name="albums">The albums to synchronize.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task SyncAlbumsToDatabase(IEnumerable<AlbumModel> albums)
    {
        foreach (var album in albums)
        {
            var existingAlbum = await _context.ImmichAlbums.FindAsync(album.Id);
            if (existingAlbum != null)
            {
                existingAlbum.Name = album.AlbumName;
                existingAlbum.PhotoCount = album.AssetCount;
                existingAlbum.LastSynced = DateTime.UtcNow;
            }
            else
            {
                _context.ImmichAlbums.Add(new ImmichAlbum
                {
                    Id = album.Id,
                    Name = album.AlbumName,
                    PhotoCount = album.AssetCount,
                    LastSynced = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Retrieves the count of locally downloaded assets for each album.
    /// </summary>
    /// <returns>A dictionary mapping album IDs to their local asset counts.</returns>
    private async Task<Dictionary<string, int>> GetLocalAssetCounts()
    {
        return await _context.DownloadedAlbums
            .ToDictionaryAsync(d => d.AlbumId, d => d.PhotoCount);
    }

    /// <summary>
    /// Removes all local assets for a specific album.
    /// Deletes the downloaded album record and all associated asset records and files.
    /// </summary>
    /// <param name="albumId">The ID of the album to remove local assets for.</param>
    /// <returns>Success status and message.</returns>
    /// <response code="200">Local assets successfully removed.</response>
    /// <response code="404">Album not found locally.</response>
    /// <response code="401">Unauthorized access.</response>
    /// <response code="500">Error removing local assets.</response>
    [HttpDelete("albums/{albumId}/local")]
    public async Task<IActionResult> RemoveLocalAssets(string albumId)
    {
        var sanitizedAlbumId = ValidateAndSanitizeFileName(albumId, "albumId");
        if (sanitizedAlbumId == null)
        {
            Logger.LogWarning("Invalid album ID '{AlbumId}' provided by user {Username}", albumId, GetCurrentUsername());
            return CreateErrorResponse(400, "Invalid album ID");
        }

        try
        {
            Logger.LogInformation("Removing local assets for album {AlbumId} by user {Username}", sanitizedAlbumId, GetCurrentUsername());

            // Find the downloaded album
            var downloadedAlbum = await _context.DownloadedAlbums
                .FirstOrDefaultAsync(da => da.AlbumId == sanitizedAlbumId);

            if (downloadedAlbum == null)
            {
                Logger.LogWarning("No local assets found for album {AlbumId} by user {Username}", sanitizedAlbumId, GetCurrentUsername());
                return CreateErrorResponse(404, "No local assets found for this album");
            }

            // Remove all asset records for this album
            var assetsToRemove = await _context.DownloadedAssets
                .Where(da => da.AlbumId == sanitizedAlbumId)
                .ToListAsync();

            if (assetsToRemove.Any())
            {
                _context.DownloadedAssets.RemoveRange(assetsToRemove);
                Logger.LogInformation("Removing {Count} asset records for album {AlbumId}", assetsToRemove.Count, sanitizedAlbumId);
            }

            // Remove the ZIP file if it exists
            if (!string.IsNullOrEmpty(downloadedAlbum.FilePath) && System.IO.File.Exists(downloadedAlbum.FilePath))
            {
                try
                {
                    System.IO.File.Delete(downloadedAlbum.FilePath);
                    Logger.LogInformation("Deleted ZIP file: {FilePath}", downloadedAlbum.FilePath);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to delete ZIP file: {FilePath}", downloadedAlbum.FilePath);
                }
            }

            // Remove the downloaded album record
            _context.DownloadedAlbums.Remove(downloadedAlbum);

            await _context.SaveChangesAsync();

            Logger.LogInformation("Successfully removed local assets for album {AlbumId} by user {Username}", sanitizedAlbumId, GetCurrentUsername());

            return Ok(new { 
                success = true, 
                message = $"Successfully removed {assetsToRemove.Count} local assets for album '{downloadedAlbum.AlbumName}'" 
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error removing local assets for album {AlbumId} by user {Username}", sanitizedAlbumId, GetCurrentUsername());
            return CreateErrorResponse(500, "Failed to remove local assets");
        }
    }

    /// <summary>
    /// Cleans up orphaned assets across all albums.
    /// Identifies and removes asset records that exist locally but not in the remote Immich server.
    /// </summary>
    /// <returns>Cleanup results including counts of orphaned assets found and removed.</returns>
    /// <response code="200">Cleanup completed successfully.</response>
    /// <response code="400">Immich configuration not set.</response>
    /// <response code="401">Unauthorized access.</response>
    /// <response code="500">Error during cleanup process.</response>
    [HttpPost("albums/cleanup-orphans")]
    public async Task<IActionResult> CleanupOrphanedAssets()
    {
        try
        {
            Logger.LogInformation("Starting orphaned asset cleanup by user {Username}", GetCurrentUsername());

            var immichUrl = await GetSettingAsync("Immich:Url");
            var apiKey = await GetSettingAsync("Immich:ApiKey");

            if (string.IsNullOrEmpty(immichUrl) || string.IsNullOrEmpty(apiKey))
            {
                Logger.LogWarning("Immich configuration not set for orphan cleanup by user {Username}", GetCurrentUsername());
                return CreateErrorResponse(400, "Immich configuration not set");
            }

            _immichService.Configure(immichUrl, apiKey);

            // Test connection first
            var (connectionSuccess, connectionMessage) = await _immichService.ValidateConnectionAsync(immichUrl, apiKey);
            if (!connectionSuccess)
            {
                Logger.LogWarning("Immich connection failed for orphan cleanup by user {Username}: {Message}", GetCurrentUsername(), connectionMessage);
                return CreateErrorResponse(500, $"Connection failed: {connectionMessage}");
            }

            // Get all albums from Immich server
            var (success, albums, error) = await _immichService.GetAlbumsAsync();
            if (!success || albums == null)
            {
                Logger.LogError("Failed to get albums for orphan cleanup by user {Username}: {Error}", GetCurrentUsername(), error);
                return CreateErrorResponse(500, error ?? "Failed to retrieve albums from Immich");
            }

            var totalOrphansFound = 0;
            var totalOrphansRemoved = 0;
            var albumsProcessed = 0;
            var albumResults = new List<object>();

            // Process each album that has local assets
            var localAlbums = await _context.DownloadedAlbums.ToListAsync();
            
            foreach (var localAlbum in localAlbums)
            {
                try
                {
                    // Find the corresponding remote album
                    var remoteAlbum = albums.FirstOrDefault(a => a.Id == localAlbum.AlbumId);
                    if (remoteAlbum == null)
                    {
                        // Entire album is orphaned - album no longer exists on remote
                        var orphanedAssets = await _context.DownloadedAssets
                            .Where(da => da.AlbumId == localAlbum.AlbumId)
                            .ToListAsync();

                        if (orphanedAssets.Any())
                        {
                            _context.DownloadedAssets.RemoveRange(orphanedAssets);
                            totalOrphansFound += orphanedAssets.Count;
                            totalOrphansRemoved += orphanedAssets.Count;

                            Logger.LogInformation("Removed {Count} orphaned assets from deleted album {AlbumId}", 
                                orphanedAssets.Count, localAlbum.AlbumId);
                        }

                        // Remove the album record
                        _context.DownloadedAlbums.Remove(localAlbum);
                        
                        // Clean up ZIP file
                        if (!string.IsNullOrEmpty(localAlbum.FilePath) && System.IO.File.Exists(localAlbum.FilePath))
                        {
                            try
                            {
                                System.IO.File.Delete(localAlbum.FilePath);
                                Logger.LogInformation("Deleted ZIP file for orphaned album: {FilePath}", localAlbum.FilePath);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogWarning(ex, "Failed to delete ZIP file: {FilePath}", localAlbum.FilePath);
                            }
                        }

                        albumResults.Add(new
                        {
                            albumId = localAlbum.AlbumId,
                            albumName = localAlbum.AlbumName,
                            status = "album_deleted",
                            orphansFound = orphanedAssets.Count,
                            orphansRemoved = orphanedAssets.Count
                        });
                    }
                    else
                    {
                        // Album exists, check for orphaned individual assets
                        var (albumSuccess, albumInfo, albumError) = await _immichService.GetAlbumInfoAsync(remoteAlbum.Id);
                        if (albumSuccess && albumInfo != null)
                        {
                            var remoteAssetIds = albumInfo.Assets
                                .Where(a => a.Type == "IMAGE")
                                .Select(a => a.Id)
                                .ToHashSet();

                            var localAssets = await _context.DownloadedAssets
                                .Where(da => da.AlbumId == localAlbum.AlbumId)
                                .ToListAsync();

                            var orphanedAssets = localAssets
                                .Where(la => !remoteAssetIds.Contains(la.AssetId))
                                .ToList();

                            if (orphanedAssets.Any())
                            {
                                _context.DownloadedAssets.RemoveRange(orphanedAssets);
                                totalOrphansFound += orphanedAssets.Count;
                                totalOrphansRemoved += orphanedAssets.Count;

                                // Update album photo count
                                localAlbum.PhotoCount = localAssets.Count - orphanedAssets.Count;

                                Logger.LogInformation("Removed {Count} orphaned assets from album {AlbumId}", 
                                    orphanedAssets.Count, localAlbum.AlbumId);
                            }

                            albumResults.Add(new
                            {
                                albumId = localAlbum.AlbumId,
                                albumName = localAlbum.AlbumName,
                                status = "processed",
                                orphansFound = orphanedAssets.Count,
                                orphansRemoved = orphanedAssets.Count
                            });
                        }
                        else
                        {
                            Logger.LogWarning("Failed to get album info for {AlbumId}: {Error}", remoteAlbum.Id, albumError);
                            albumResults.Add(new
                            {
                                albumId = localAlbum.AlbumId,
                                albumName = localAlbum.AlbumName,
                                status = "error",
                                error = albumError,
                                orphansFound = 0,
                                orphansRemoved = 0
                            });
                        }
                    }

                    albumsProcessed++;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error processing album {AlbumId} during orphan cleanup", localAlbum.AlbumId);
                    albumResults.Add(new
                    {
                        albumId = localAlbum.AlbumId,
                        albumName = localAlbum.AlbumName,
                        status = "error",
                        error = ex.Message,
                        orphansFound = 0,
                        orphansRemoved = 0
                    });
                }
            }

            // Save all changes
            await _context.SaveChangesAsync();

            Logger.LogInformation("Orphan cleanup completed by user {Username}: {AlbumsProcessed} albums processed, {OrphansFound} orphans found, {OrphansRemoved} orphans removed", 
                GetCurrentUsername(), albumsProcessed, totalOrphansFound, totalOrphansRemoved);

            return Ok(new
            {
                success = true,
                message = $"Cleanup completed: {totalOrphansRemoved} orphaned assets removed from {albumsProcessed} albums",
                summary = new
                {
                    albumsProcessed,
                    orphansFound = totalOrphansFound,
                    orphansRemoved = totalOrphansRemoved
                },
                details = albumResults
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during orphaned asset cleanup by user {Username}", GetCurrentUsername());
            return CreateErrorResponse(500, "Failed to cleanup orphaned assets");
        }
    }

    /// <summary>
    /// Retrieves a configuration setting from the database.
    /// </summary>
    /// <param name="key">The setting key to retrieve.</param>
    /// <returns>The setting value if found, otherwise null.</returns>
    private async Task<string?> GetSettingAsync(string key)
    {
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value;
    }
}