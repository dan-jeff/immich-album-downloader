using System;
using Newtonsoft.Json;

namespace Immich.Data.Models;

/// <summary>
/// Represents an album model returned from the Immich API.
/// Contains basic album information including metadata and asset counts.
/// </summary>
public class AlbumModel
{
    /// <summary>
    /// Gets or sets the name of the album as defined in the Immich server.
    /// This is the display name shown to users in the Immich interface.
    /// </summary>
    [JsonProperty("albumName")]
    public required string AlbumName { set; get; }
    
    /// <summary>
    /// Gets or sets the unique identifier for the album in the Immich system.
    /// This ID is used for all API operations related to the album.
    /// </summary>
    [JsonProperty("id")]
    public required string Id { set; get; }
    
    /// <summary>
    /// Gets or sets the total number of assets (photos and videos) in the album.
    /// This count includes all media types supported by Immich.
    /// </summary>
    [JsonProperty("assetCount")]
    public int AssetCount { set; get; }
    
    /// <summary>
    /// Gets or sets the asset ID of the image used as the album thumbnail.
    /// This references another asset within the album that serves as the cover image.
    /// May be empty if no thumbnail is set.
    /// </summary>
    [JsonProperty("albumThumbnailAssetId")]
    public string AlbumThumbnailAssetId { set; get; } = string.Empty;
}