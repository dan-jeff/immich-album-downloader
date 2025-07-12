using ImmichDownloader.Web.Models;

namespace ImmichDownloader.Web.Services.Repositories;

public interface IResizedImageRepository
{
    Task<ResizedImage?> GetByIdAsync(int id);
    Task<ResizedImage?> GetByImageAndJobAsync(int imageId, int resizeJobId);
    Task<IEnumerable<ResizedImage>> GetByImageIdAsync(int imageId);
    Task<IEnumerable<ResizedImage>> GetByJobIdAsync(int resizeJobId);
    Task<IEnumerable<ResizedImage>> GetByStatusAsync(string status);
    Task<IEnumerable<ResizedImage>> GetCompletedByJobAsync(int resizeJobId);
    Task<ResizedImage> CreateAsync(ResizedImage resizedImage);
    Task<ResizedImage> UpdateAsync(ResizedImage resizedImage);
    Task DeleteAsync(int id);
    Task DeleteByJobIdAsync(int resizeJobId);
    Task<bool> ExistsAsync(int imageId, int resizeJobId);
    Task<int> GetCountByJobAsync(int resizeJobId, string? status = null);
    Task UpdateStatusAsync(int id, string status, string? error = null);
    Task<IEnumerable<ResizedImage>> GetOrphanedResizedImagesAsync();
}