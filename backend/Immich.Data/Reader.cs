using System;
using Immich.Data.Models;
using Newtonsoft.Json;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Immich.Data;

public class Reader
{
    private readonly ReaderConfiguration _readerConfiguration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<Reader>? _logger;

    public Reader(ReaderConfiguration readerConfiguration, IHttpClientFactory httpClientFactory, ILogger<Reader>? logger = null)
    {
        _readerConfiguration = readerConfiguration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<AlbumModel>> GetAlbums()
    {
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(_readerConfiguration.BaseAddress);
        httpClient.DefaultRequestHeaders.Add("x-api-key", _readerConfiguration.ApiKey);

        var httpResponse = await httpClient.GetAsync("api/albums");

        httpResponse.EnsureSuccessStatusCode();

        var json = await httpResponse.Content.ReadAsStringAsync();

        // Debug logging to see what we're getting from the API
        _logger?.LogInformation("=== JSON Response from Immich API ===");
        _logger?.LogInformation("{JsonResponse}", json);
        _logger?.LogInformation("=== End JSON Response ===");

        var settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc
        };
        
        var albums = JsonConvert.DeserializeObject<List<AlbumModel>>(json, settings);
        
        // Debug logging to see what was deserialized
        _logger?.LogInformation("=== Deserialized Albums ===");
        foreach (var album in albums ?? new List<AlbumModel>())
        {
            _logger?.LogInformation("Album: {AlbumName}, AssetCount: {AssetCount}, AlbumThumbnailAssetId: '{AlbumThumbnailAssetId}'", 
                album.AlbumName, album.AssetCount, album.AlbumThumbnailAssetId ?? "null");
        }
        _logger?.LogInformation("=== End Deserialized Albums ===");

        return albums;
    }

    public async Task<AlbumInfoModel> GetAlbumInfo(AlbumModel album)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(_readerConfiguration.BaseAddress);
        httpClient.DefaultRequestHeaders.Add("x-api-key", _readerConfiguration.ApiKey);

        // Get album details first
        var albumResponse = await httpClient.GetAsync($"api/albums/{album.Id}");
        albumResponse.EnsureSuccessStatusCode();
        var albumJson = await albumResponse.Content.ReadAsStringAsync();
        
        Console.WriteLine($"DEBUG: Album details response: {albumJson}");

        // Try to deserialize as album object first
        dynamic albumData;
        try
        {
            albumData = JsonConvert.DeserializeObject(albumJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Failed to parse album JSON: {ex.Message}");
            throw new InvalidOperationException($"Failed to parse album response: {ex.Message}");
        }

        // Handle different response formats
        if (albumData is Newtonsoft.Json.Linq.JArray)
        {
            // If it's an array, it might be returning assets directly
            var assets = JsonConvert.DeserializeObject<AlbumInfoAssetModel[]>(albumJson);
            return new AlbumInfoModel
            {
                AlbumName = album.AlbumName,
                Description = "",
                Assets = assets ?? Array.Empty<AlbumInfoAssetModel>()
            };
        }
        else
        {
            // If it's an object, extract album info and get assets separately
            string albumName = albumData?.albumName ?? album.AlbumName;
            string description = albumData?.description ?? "";
            
            // Try to get assets from the album object, or fetch them separately
            AlbumInfoAssetModel[] assets;
            if (albumData?.assets != null)
            {
                assets = JsonConvert.DeserializeObject<AlbumInfoAssetModel[]>(albumData.assets.ToString());
            }
            else
            {
                // Fetch assets separately (common Immich API pattern)
                var assetsResponse = await httpClient.GetAsync($"api/albums/{album.Id}/assets");
                if (assetsResponse.IsSuccessStatusCode)
                {
                    var assetsJson = await assetsResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"DEBUG: Assets response: {assetsJson}");
                    assets = JsonConvert.DeserializeObject<AlbumInfoAssetModel[]>(assetsJson) ?? Array.Empty<AlbumInfoAssetModel>();
                }
                else
                {
                    assets = Array.Empty<AlbumInfoAssetModel>();
                }
            }

            return new AlbumInfoModel
            {
                AlbumName = albumName,
                Description = description,
                Assets = assets
            };
        }
    }

    public async Task<byte[]> DownloadAsset(AlbumInfoAssetModel albumInfoAssetModel)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(_readerConfiguration.BaseAddress);
        httpClient.DefaultRequestHeaders.Add("x-api-key", _readerConfiguration.ApiKey);

        var httpResponse = await httpClient.GetAsync("api/assets/" + albumInfoAssetModel.Id + "/original");

        httpResponse.EnsureSuccessStatusCode();

        return await httpResponse.Content.ReadAsByteArrayAsync();
    }
}

public class ReaderConfiguration
{
    public required string BaseAddress { set; get; }
    public required string ApiKey { set; get; }
}