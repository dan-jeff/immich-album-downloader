using ImmichDownloader.Web.Models;

namespace ImmichDownloader.Web.Services.Sync;

public interface IAlbumSyncService
{
    Task<AlbumSyncResult> SyncAlbumAsync(string immichAlbumId);
    Task<AlbumSyncResult> SyncAlbumFromMetadataAsync(Album albumMetadata, IEnumerable<string> assetIds);
    Task<IEnumerable<Album>> GetAlbumsNeedingSyncAsync();
    Task<SyncSummary> SyncAllAlbumsAsync();
    Task<bool> IsAlbumSyncedAsync(string immichAlbumId);
    Task<DateTime?> GetLastSyncTimeAsync(string immichAlbumId);
}

public class AlbumSyncResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Album? Album { get; set; }
    public int TotalAssets { get; set; }
    public int NewAssets { get; set; }
    public int ExistingAssets { get; set; }
    public int RemovedAssets { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime SyncTime { get; set; } = DateTime.UtcNow;
}

public class SyncSummary
{
    public int TotalAlbums { get; set; }
    public int SuccessfulSyncs { get; set; }
    public int FailedSyncs { get; set; }
    public int TotalNewAssets { get; set; }
    public int TotalRemovedAssets { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public List<string> Errors { get; set; } = new();
}