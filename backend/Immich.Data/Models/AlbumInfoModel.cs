using System;

namespace Immich.Data.Models;

/// <summary>
/// Represents detailed album information returned from the Immich API.
/// Contains complete album metadata including all assets within the album.
/// </summary>
public class AlbumInfoModel
{
    /// <summary>
    /// Gets or sets the name of the album as defined in the Immich server.
    /// This is the display name shown to users in the Immich interface.
    /// </summary>
    public required string AlbumName { set; get; }
    
    /// <summary>
    /// Gets or sets the description of the album as defined in the Immich server.
    /// This is optional text that provides additional context about the album.
    /// </summary>
    public required string Description { set; get; }
    
    /// <summary>
    /// Gets or sets the array of assets (photos and videos) contained in the album.
    /// Each asset includes metadata necessary for downloading and processing.
    /// </summary>
    public required AlbumInfoAssetModel[] Assets { set; get; }
}

/// <summary>
/// Represents an individual asset (photo or video) within an album from the Immich API.
/// Contains the essential metadata needed to download and process the asset.
/// </summary>
public class AlbumInfoAssetModel
{
    /// <summary>
    /// Gets or sets the unique identifier for the asset in the Immich system.
    /// This ID is used for all API operations related to the asset.
    /// </summary>
    public required string Id { set; get; }
    
    /// <summary>
    /// Gets or sets the type of the asset (e.g., "IMAGE", "VIDEO").
    /// This indicates the media type and determines how the asset should be processed.
    /// </summary>
    public required string Type { set; get; } // IMAGE
    
    /// <summary>
    /// Gets or sets the original file name of the asset as it was uploaded to Immich.
    /// This is the filename that will be used when downloading the asset.
    /// </summary>
    public required string OriginalFileName { set; get; } // originalFileName
}
