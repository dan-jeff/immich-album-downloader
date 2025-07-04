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
public class AlbumsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IImmichService _immichService;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AlbumsController> _logger;

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
        ILogger<AlbumsController> logger)
    {
        _context = context;
        _immichService = immichService;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
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
        
        _logger.LogInformation("Fetching albums - URL: '{Url}', API Key set: {ApiKeySet}", 
            immichUrl, !string.IsNullOrEmpty(apiKey));

        if (string.IsNullOrEmpty(immichUrl) || string.IsNullOrEmpty(apiKey))
            return BadRequest(new { detail = "Configuration not set" });

        _immichService.Configure(immichUrl, apiKey);

        var (success, albums, error) = await _immichService.GetAlbumsAsync();
        if (!success)
        {
            _logger.LogError("Failed to get albums: {Error}", error);
            return StatusCode(500, new { detail = error });
        }

        // Debug logging to see what ImmichService returned
        _logger.LogInformation("=== Albums returned from ImmichService ===");
        foreach (var album in albums ?? new List<AlbumModel>())
        {
            _logger.LogInformation("Album: {AlbumName}, AssetCount: {AssetCount}, AlbumThumbnailAssetId: '{AlbumThumbnailAssetId}'", 
                album.AlbumName, album.AssetCount, album.AlbumThumbnailAssetId ?? "null");
        }
        _logger.LogInformation("=== End Albums from ImmichService ===");

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
                _logger.LogError(ex, "Error getting live stats, returning cached data");
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
        var immichUrl = await GetSettingAsync("Immich:Url");
        var apiKey = await GetSettingAsync("Immich:ApiKey");

        if (string.IsNullOrEmpty(immichUrl) || string.IsNullOrEmpty(apiKey))
            return BadRequest(new { detail = "Configuration not set" });

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

            var baseUrl = immichUrl.EndsWith('/') ? immichUrl : immichUrl + "/";
            var thumbnailUrl = $"{baseUrl}api/assets/{assetId}/thumbnail?size=preview";

            var response = await httpClient.GetAsync(thumbnailUrl);
            if (!response.IsSuccessStatusCode)
                return NotFound(new { detail = "Thumbnail not found" });

            var content = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";

            Response.Headers.Append("Cache-Control", "max-age=86400"); // Cache for 1 day
            return File(content, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying thumbnail for asset {AssetId}", assetId);
            return NotFound(new { detail = "Thumbnail not found" });
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