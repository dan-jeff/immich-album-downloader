using Immich.Data.Models;
using ImmichDownloader.Web.Models.Requests;

namespace ImmichDownloader.Web.Services;

/// <summary>
/// Service interface for interacting with the Immich photo management server.
/// Provides methods for authentication, album management, and asset downloading.
/// </summary>
public interface IImmichService
{
    /// <summary>
    /// Validates the connection to an Immich server using the provided URL and API key.
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
    Task<(bool Success, string Message)> ValidateConnectionAsync(string url, string apiKey);

    /// <summary>
    /// Retrieves all albums from the configured Immich server.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous get albums operation. The task result contains:
    /// - Success: true if albums were retrieved successfully, false otherwise
    /// - Albums: collection of album models if successful, null otherwise
    /// - Error: error message if the operation failed, null otherwise
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the service is not properly configured.</exception>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request to the server fails.</exception>
    Task<(bool Success, IEnumerable<AlbumModel>? Albums, string? Error)> GetAlbumsAsync();

    /// <summary>
    /// Retrieves detailed information about a specific album from the Immich server.
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
    Task<(bool Success, AlbumInfoModel? Album, string? Error)> GetAlbumInfoAsync(string albumId);

    /// <summary>
    /// Downloads the binary data of a specific asset from the Immich server.
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
    Task<(bool Success, byte[]? Data, string? Error)> DownloadAssetAsync(string assetId);

    /// <summary>
    /// Configures the service with the Immich server URL and API key.
    /// This method must be called before using other service methods.
    /// </summary>
    /// <param name="url">The base URL of the Immich server (e.g., "https://immich.example.com").</param>
    /// <param name="apiKey">The API key for authentication with the Immich server.</param>
    /// <exception cref="ArgumentException">Thrown when the URL or API key is null or empty.</exception>
    void Configure(string url, string apiKey);
}

