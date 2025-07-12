using ImmichDownloader.Web.Models;

namespace ImmichDownloader.Web.Services.Repositories;

public interface IAlbumRepository
{
    Task<Album?> GetByIdAsync(int id);
    Task<Album?> GetByImmichIdAsync(string immichId);
    Task<IEnumerable<Album>> GetActiveAlbumsAsync();
    Task<IEnumerable<Album>> GetAlbumsByStatusAsync(string syncStatus);
    Task<IEnumerable<Album>> GetAlbumsNeedingSyncAsync();
    Task<Album> CreateAsync(Album album);
    Task<Album> UpdateAsync(Album album);
    Task DeleteAsync(int id);
    Task DeactivateAsync(int id);
    Task<bool> ExistsAsync(string immichId);
    Task<int> GetTotalCountAsync();
    Task<int> GetActiveCountAsync();
    Task UpdateSyncStatusAsync(int id, string status, string? error = null);
}