using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace ImmichDownloader.Web.Services.Repositories;

public class ResizeJobRepository : IResizeJobRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ResizeJobRepository> _logger;

    public ResizeJobRepository(ApplicationDbContext context, ILogger<ResizeJobRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResizeJob?> GetByIdAsync(int id)
    {
        try
        {
            return await _context.ResizeJobs
                .Include(rj => rj.Album)
                .Include(rj => rj.ResizeProfile)
                .Include(rj => rj.ResizedImages)
                .AsNoTracking()
                .FirstOrDefaultAsync(rj => rj.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resize job by ID {JobId}", id);
            throw;
        }
    }

    public async Task<ResizeJob?> GetByJobIdAsync(string jobId)
    {
        try
        {
            return await _context.ResizeJobs
                .Include(rj => rj.Album)
                .Include(rj => rj.ResizeProfile)
                .Include(rj => rj.ResizedImages)
                .AsNoTracking()
                .FirstOrDefaultAsync(rj => rj.JobId == jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resize job by Job ID {JobId}", jobId);
            throw;
        }
    }

    public async Task<IEnumerable<ResizeJob>> GetByStatusAsync(string status)
    {
        try
        {
            return await _context.ResizeJobs
                .Include(rj => rj.Album)
                .Include(rj => rj.ResizeProfile)
                .Where(rj => rj.Status == status)
                .OrderBy(rj => rj.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resize jobs by status {Status}", status);
            throw;
        }
    }

    public async Task<IEnumerable<ResizeJob>> GetByAlbumIdAsync(int albumId)
    {
        try
        {
            return await _context.ResizeJobs
                .Include(rj => rj.ResizeProfile)
                .Include(rj => rj.ResizedImages)
                .Where(rj => rj.AlbumId == albumId)
                .OrderByDescending(rj => rj.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resize jobs for album {AlbumId}", albumId);
            throw;
        }
    }

    public async Task<IEnumerable<ResizeJob>> GetByProfileIdAsync(int profileId)
    {
        try
        {
            return await _context.ResizeJobs
                .Include(rj => rj.Album)
                .Include(rj => rj.ResizedImages)
                .Where(rj => rj.ResizeProfileId == profileId)
                .OrderByDescending(rj => rj.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resize jobs for profile {ProfileId}", profileId);
            throw;
        }
    }

    public async Task<ResizeJob?> GetExistingJobAsync(int? albumId, int profileId)
    {
        try
        {
            return await _context.ResizeJobs
                .Include(rj => rj.ResizeProfile)
                .Where(rj => rj.AlbumId == albumId && 
                            rj.ResizeProfileId == profileId && 
                            rj.Status == "completed")
                .OrderByDescending(rj => rj.CompletedAt)
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for existing resize job for album {AlbumId} and profile {ProfileId}", 
                albumId, profileId);
            throw;
        }
    }

    public async Task<IEnumerable<ResizeJob>> GetPendingJobsAsync()
    {
        try
        {
            return await _context.ResizeJobs
                .Include(rj => rj.Album)
                .Include(rj => rj.ResizeProfile)
                .Where(rj => rj.Status == "pending")
                .OrderBy(rj => rj.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending resize jobs");
            throw;
        }
    }

    public async Task<IEnumerable<ResizeJob>> GetCompletedJobsAsync()
    {
        try
        {
            return await _context.ResizeJobs
                .Include(rj => rj.Album)
                .Include(rj => rj.ResizeProfile)
                .Where(rj => rj.Status == "completed")
                .OrderByDescending(rj => rj.CompletedAt)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving completed resize jobs");
            throw;
        }
    }

    public async Task<ResizeJob> CreateAsync(ResizeJob resizeJob)
    {
        try
        {
            resizeJob.CreatedAt = DateTime.UtcNow;
            resizeJob.UpdatedAt = DateTime.UtcNow;

            _context.ResizeJobs.Add(resizeJob);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Created resize job {JobId} for album {AlbumId} with profile {ProfileId}", 
                resizeJob.JobId, resizeJob.AlbumId, resizeJob.ResizeProfileId);
            return resizeJob;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating resize job {JobId}", resizeJob.JobId);
            throw;
        }
    }

    public async Task<ResizeJob> UpdateAsync(ResizeJob resizeJob)
    {
        try
        {
            resizeJob.UpdatedAt = DateTime.UtcNow;

            _context.ResizeJobs.Update(resizeJob);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Updated resize job {JobId}", resizeJob.JobId);
            return resizeJob;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating resize job {JobId}", resizeJob.JobId);
            throw;
        }
    }

    public async Task DeleteAsync(int id)
    {
        try
        {
            var resizeJob = await _context.ResizeJobs.FindAsync(id);
            if (resizeJob != null)
            {
                _context.ResizeJobs.Remove(resizeJob);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Deleted resize job {JobId}", id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting resize job {JobId}", id);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string jobId)
    {
        try
        {
            return await _context.ResizeJobs.AnyAsync(rj => rj.JobId == jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if resize job exists with Job ID {JobId}", jobId);
            throw;
        }
    }

    public async Task<int> GetTotalCountAsync()
    {
        try
        {
            return await _context.ResizeJobs.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting total resize job count");
            throw;
        }
    }

    public async Task UpdateStatusAsync(int id, string status, string? error = null)
    {
        try
        {
            var resizeJob = await _context.ResizeJobs.FindAsync(id);
            if (resizeJob != null)
            {
                resizeJob.Status = status;
                resizeJob.ErrorMessage = error;
                resizeJob.UpdatedAt = DateTime.UtcNow;
                
                if (status == "processing" && resizeJob.StartedAt == null)
                {
                    resizeJob.StartedAt = DateTime.UtcNow;
                }
                else if (status == "completed" || status == "error")
                {
                    resizeJob.CompletedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Updated status for resize job {JobId} to {Status}", id, status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for resize job {JobId}", id);
            throw;
        }
    }

    public async Task UpdateProgressAsync(int id, int processed, int failed, int skipped)
    {
        try
        {
            var resizeJob = await _context.ResizeJobs.FindAsync(id);
            if (resizeJob != null)
            {
                resizeJob.ProcessedImages = processed;
                resizeJob.FailedImages = failed;
                resizeJob.SkippedImages = skipped;
                resizeJob.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogDebug("Updated progress for resize job {JobId}: {Processed}/{Total} processed", 
                    id, processed, resizeJob.TotalImages);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating progress for resize job {JobId}", id);
            throw;
        }
    }
}