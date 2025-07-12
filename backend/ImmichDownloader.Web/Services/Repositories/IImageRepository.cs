using ImmichDownloader.Web.Models;

namespace ImmichDownloader.Web.Services.Repositories;

public interface IImageRepository
{
    Task<Image?> GetByIdAsync(int id);
    Task<Image?> GetByImmichIdAsync(string immichId);
    Task<IEnumerable<Image>> GetByImmichIdsAsync(IEnumerable<string> immichIds);
    Task<IEnumerable<Image>> GetDownloadedImagesAsync(int skip = 0, int take = 100);
    Task<IEnumerable<Image>> GetPendingDownloadsAsync(int skip = 0, int take = 100);
    Task<IEnumerable<Image>> GetImagesByAlbumIdAsync(int albumId, bool activeOnly = true);
    Task<IEnumerable<Image>> GetDanglingImagesAsync();
    Task<Image> CreateAsync(Image image);
    Task<Image> UpdateAsync(Image image);
    Task DeleteAsync(int id);
    Task DeleteByImmichIdAsync(string immichId);
    Task<bool> ExistsAsync(string immichId);
    Task<int> GetTotalCountAsync();
    Task<int> GetDownloadedCountAsync();
}