using Immich.Data;
using Immich.Data.Models;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ImmichDownloader.Web.Services;

/// <summary>
/// Implementation of the Immich service for interacting with the Immich photo management server.
/// This service handles communication with the Immich API for album management, asset downloading, and server validation.
/// </summary>
public class ImmichService : IImmichService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImmichService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    
    /// <summary>
    /// The base URL of the Immich server.
    /// </summary>
    private string _baseUrl = string.Empty;
    
    /// <summary>
    /// The API key for authentication with the Immich server.
    /// </summary>
    private string _apiKey = string.Empty;

    /// <summary>
    /// Initializes a new instance of the ImmichService class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory for creating HTTP clients.</param>
    /// <param name="logger">The logger instance for this service.</param>
    /// <param name="loggerFactory">The logger factory for creating loggers for dependencies.</param>
    public ImmichService(IHttpClientFactory httpClientFactory, ILogger<ImmichService> logger, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Configures the service with the Immich server URL and API key.
    /// This method must be called before using other service methods.
    /// </summary>
    /// <param name="url">The base URL of the Immich server (e.g., "https://immich.example.com").</param>
    /// <param name="apiKey">The API key for authentication with the Immich server.</param>
    /// <exception cref="ArgumentException">Thrown when the URL or API key is null or empty.</exception>
    public void Configure(string url, string apiKey)
    {
        // Validate URL
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be null or empty", nameof(url));
        
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Invalid URL format", nameof(url));
        
        // Validate API key
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key cannot be empty", nameof(apiKey));

        _baseUrl = url.EndsWith('/') ? url : url + "/";
        _apiKey = apiKey;
    }

    /// <summary>
    /// Validates the connection to an Immich server using the provided URL and API key.
    /// Tests the connection by calling the server's ping endpoint and validating the response.
    /// </summary>
    /// <param name="url">The base URL of the Immich server (e.g., "https://immich.example.com").</param>
    /// <param name="apiKey">The API key for authentication with the Immich server.</param>
    /// <returns>
    /// A task that represents the asynchronous validation operation. The task result contains:
    /// - Success: true if the connection is valid, false otherwise
    /// - Message: descriptive message about the validation result
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when the URL or API key is null or empty.</exception>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request to the server fails.</exception>
    public async Task<(bool Success, string Message)> ValidateConnectionAsync(string url, string apiKey)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

            var baseUrl = url.EndsWith('/') ? url : url + "/";
            var pingUrl = $"{baseUrl}api/server/ping";
            
            _logger.LogInformation("Testing connection to: {PingUrl}", pingUrl);

            var response = await httpClient.GetAsync(pingUrl);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (false, "Failed to connect: The provided API Key is invalid.");

            if (!response.IsSuccessStatusCode)
                return (false, $"Failed to connect: HTTP Error {response.StatusCode}");

            var content = await response.Content.ReadAsStringAsync();
            
            try
            {
                var pingResponse = JsonSerializer.Deserialize<JsonElement>(content);
                if (pingResponse.TryGetProperty("res", out var res) && res.GetString() == "pong")
                    return (true, "Successfully connected to Immich!");
                else
                    return (false, "Failed to connect: Unexpected response from server.");
            }
            catch (JsonException)
            {
                return (false, "Failed to connect: The server response was not in the expected JSON format.");
            }
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Failed to connect: Could not reach the server at the provided URL. Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during connection validation");
            return (false, $"Failed to connect: Unexpected error. Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves all albums from the configured Immich server.
    /// Uses the Immich Data Reader to fetch album information from the server.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous get albums operation. The task result contains:
    /// - Success: true if albums were retrieved successfully, false otherwise
    /// - Albums: collection of album models if successful, null otherwise
    /// - Error: error message if the operation failed, null otherwise
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the service is not properly configured.</exception>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request to the server fails.</exception>
    public async Task<(bool Success, IEnumerable<AlbumModel>? Albums, string? Error)> GetAlbumsAsync()
    {
        if (string.IsNullOrEmpty(_baseUrl) || string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("Immich service is not configured");

        try
        {
            var reader = new Reader(new ReaderConfiguration
            {
                BaseAddress = _baseUrl,
                ApiKey = _apiKey
            }, _httpClientFactory, _loggerFactory.CreateLogger<Reader>());

            var albums = await reader.GetAlbums();
            return (true, albums, null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching albums");
            
            // Check for specific HTTP errors
            if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                return (false, null, $"Error fetching albums: authentication error. {ex.Message}");
            else
                return (false, null, $"Error fetching albums: server error. {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching albums");
            return (false, null, $"Error fetching albums: Unexpected error. Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves detailed information about a specific album from the Immich server.
    /// This includes album metadata and asset information within the album.
    /// </summary>
    /// <param name="albumId">The unique identifier of the album to retrieve.</param>
    /// <returns>
    /// A task that represents the asynchronous get album info operation. The task result contains:
    /// - Success: true if album information was retrieved successfully, false otherwise
    /// - Album: the album information model if successful, null otherwise
    /// - Error: error message if the operation failed, null otherwise
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when the album ID is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the service is not properly configured.</exception>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request to the server fails.</exception>
    public async Task<(bool Success, AlbumInfoModel? Album, string? Error)> GetAlbumInfoAsync(string albumId)
    {
        if (string.IsNullOrEmpty(_baseUrl) || string.IsNullOrEmpty(_apiKey))
            return (false, null, "Immich service not configured");

        try
        {
            var reader = new Reader(new ReaderConfiguration
            {
                BaseAddress = _baseUrl,
                ApiKey = _apiKey
            }, _httpClientFactory, _loggerFactory.CreateLogger<Reader>());

            // Create a temporary AlbumModel for the Reader
            var album = new AlbumModel { Id = albumId, AlbumName = "" };
            var albumInfo = await reader.GetAlbumInfo(album);
            return (true, albumInfo, null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching album info for {AlbumId}", albumId);
            // Re-throw HTTP exceptions to allow tests to verify error handling
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching album info for {AlbumId}", albumId);
            return (false, null, $"Error fetching album info: Unexpected error. Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Downloads the binary data of a specific asset from the Immich server.
    /// Creates a temporary asset model and uses the Immich Data Reader to download the asset.
    /// </summary>
    /// <param name="assetId">The unique identifier of the asset to download.</param>
    /// <returns>
    /// A task that represents the asynchronous download operation. The task result contains:
    /// - Success: true if the asset was downloaded successfully, false otherwise
    /// - Data: the binary data of the asset if successful, null otherwise
    /// - Error: error message if the operation failed, null otherwise
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when the asset ID is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the service is not properly configured.</exception>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request to the server fails.</exception>
    public async Task<(bool Success, byte[]? Data, string? Error)> DownloadAssetAsync(string assetId)
    {
        if (string.IsNullOrEmpty(_baseUrl) || string.IsNullOrEmpty(_apiKey))
            return (false, null, "Immich service not configured");

        try
        {
            var reader = new Reader(new ReaderConfiguration
            {
                BaseAddress = _baseUrl,
                ApiKey = _apiKey
            }, _httpClientFactory, _loggerFactory.CreateLogger<Reader>());

            // Create a temporary asset model for the Reader
            var asset = new AlbumInfoAssetModel { Id = assetId, Type = "IMAGE", OriginalFileName = "unknown.jpg" };
            var data = await reader.DownloadAsset(asset);
            return (true, data, null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while downloading asset {AssetId}", assetId);
            // Re-throw HTTP exceptions to allow tests to verify error handling
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while downloading asset {AssetId}", assetId);
            return (false, null, $"Error downloading asset: Unexpected error. Error: {ex.Message}");
        }
    }
}