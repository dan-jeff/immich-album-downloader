using ImmichDownloader.Web.Models;

namespace ImmichDownloader.Web.Services.Repositories;

public interface IImageAlbumRepository
{
    Task<ImageAlbum?> GetByIdAsync(int id);
    Task<ImageAlbum?> GetByImageAndAlbumAsync(int imageId, int albumId);
    Task<IEnumerable<ImageAlbum>> GetByAlbumIdAsync(int albumId, bool activeOnly = true);
    Task<IEnumerable<ImageAlbum>> GetByImageIdAsync(int imageId, bool activeOnly = true);
    Task<ImageAlbum> CreateAsync(ImageAlbum imageAlbum);
    Task<ImageAlbum> UpdateAsync(ImageAlbum imageAlbum);
    Task DeleteAsync(int id);
    Task DeactivateAsync(int imageId, int albumId);
    Task ActivateAsync(int imageId, int albumId);
    Task<bool> ExistsAsync(int imageId, int albumId);
    Task<int> GetCountByAlbumAsync(int albumId, bool activeOnly = true);
    Task RemoveImageFromAlbumAsync(int imageId, int albumId);
    Task<IEnumerable<ImageAlbum>> GetInactiveRelationshipsAsync();
}