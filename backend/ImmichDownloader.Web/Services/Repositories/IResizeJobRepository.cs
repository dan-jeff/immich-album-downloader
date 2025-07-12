using ImmichDownloader.Web.Models;

namespace ImmichDownloader.Web.Services.Repositories;

public interface IResizeJobRepository
{
    Task<ResizeJob?> GetByIdAsync(int id);
    Task<ResizeJob?> GetByJobIdAsync(string jobId);
    Task<IEnumerable<ResizeJob>> GetByStatusAsync(string status);
    Task<IEnumerable<ResizeJob>> GetByAlbumIdAsync(int albumId);
    Task<IEnumerable<ResizeJob>> GetByProfileIdAsync(int profileId);
    Task<ResizeJob?> GetExistingJobAsync(int? albumId, int profileId);
    Task<IEnumerable<ResizeJob>> GetPendingJobsAsync();
    Task<IEnumerable<ResizeJob>> GetCompletedJobsAsync();
    Task<ResizeJob> CreateAsync(ResizeJob resizeJob);
    Task<ResizeJob> UpdateAsync(ResizeJob resizeJob);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(string jobId);
    Task<int> GetTotalCountAsync();
    Task UpdateStatusAsync(int id, string status, string? error = null);
    Task UpdateProgressAsync(int id, int processed, int failed, int skipped);
}