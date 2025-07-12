using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace ImmichDownloader.Web.Services.Repositories;

public class ResizedImageRepository : IResizedImageRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ResizedImageRepository> _logger;

    public ResizedImageRepository(ApplicationDbContext context, ILogger<ResizedImageRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResizedImage?> GetByIdAsync(int id)
    {
        try
        {
            return await _context.ResizedImages
                .Include(ri => ri.Image)
                .Include(ri => ri.ResizeJob)
                .ThenInclude(rj => rj.ResizeProfile)
                .AsNoTracking()
                .FirstOrDefaultAsync(ri => ri.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resized image by ID {Id}", id);
            throw;
        }
    }

    public async Task<ResizedImage?> GetByImageAndJobAsync(int imageId, int resizeJobId)
    {
        try
        {
            return await _context.ResizedImages
                .Include(ri => ri.Image)
                .Include(ri => ri.ResizeJob)
                .AsNoTracking()
                .FirstOrDefaultAsync(ri => ri.ImageId == imageId && ri.ResizeJobId == resizeJobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resized image for image {ImageId} and job {ResizeJobId}", 
                imageId, resizeJobId);
            throw;
        }
    }

    public async Task<IEnumerable<ResizedImage>> GetByImageIdAsync(int imageId)
    {
        try
        {
            return await _context.ResizedImages
                .Include(ri => ri.ResizeJob)
                .ThenInclude(rj => rj.ResizeProfile)
                .Where(ri => ri.ImageId == imageId)
                .OrderByDescending(ri => ri.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resized images for image {ImageId}", imageId);
            throw;
        }
    }

    public async Task<IEnumerable<ResizedImage>> GetByJobIdAsync(int resizeJobId)
    {
        try
        {
            return await _context.ResizedImages
                .Include(ri => ri.Image)
                .Where(ri => ri.ResizeJobId == resizeJobId)
                .OrderBy(ri => ri.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resized images for job {ResizeJobId}", resizeJobId);
            throw;
        }
    }

    public async Task<IEnumerable<ResizedImage>> GetByStatusAsync(string status)
    {
        try
        {
            return await _context.ResizedImages
                .Include(ri => ri.Image)
                .Include(ri => ri.ResizeJob)
                .Where(ri => ri.Status == status)
                .OrderBy(ri => ri.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resized images by status {Status}", status);
            throw;
        }
    }

    public async Task<IEnumerable<ResizedImage>> GetCompletedByJobAsync(int resizeJobId)
    {
        try
        {
            return await _context.ResizedImages
                .Include(ri => ri.Image)
                .Where(ri => ri.ResizeJobId == resizeJobId && ri.Status == "completed")
                .OrderBy(ri => ri.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving completed resized images for job {ResizeJobId}", resizeJobId);
            throw;
        }
    }

    public async Task<ResizedImage> CreateAsync(ResizedImage resizedImage)
    {
        try
        {
            resizedImage.CreatedAt = DateTime.UtcNow;

            _context.ResizedImages.Add(resizedImage);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Created resized image for image {ImageId} in job {ResizeJobId}", 
                resizedImage.ImageId, resizedImage.ResizeJobId);
            return resizedImage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating resized image for image {ImageId} in job {ResizeJobId}", 
                resizedImage.ImageId, resizedImage.ResizeJobId);
            throw;
        }
    }

    public async Task<ResizedImage> UpdateAsync(ResizedImage resizedImage)
    {
        try
        {
            _context.ResizedImages.Update(resizedImage);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Updated resized image {Id}", resizedImage.Id);
            return resizedImage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating resized image {Id}", resizedImage.Id);
            throw;
        }
    }

    public async Task DeleteAsync(int id)
    {
        try
        {
            var resizedImage = await _context.ResizedImages.FindAsync(id);
            if (resizedImage != null)
            {
                _context.ResizedImages.Remove(resizedImage);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Deleted resized image {Id}", id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting resized image {Id}", id);
            throw;
        }
    }

    public async Task DeleteByJobIdAsync(int resizeJobId)
    {
        try
        {
            var resizedImages = await _context.ResizedImages
                .Where(ri => ri.ResizeJobId == resizeJobId)
                .ToListAsync();

            if (resizedImages.Any())
            {
                _context.ResizedImages.RemoveRange(resizedImages);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Deleted {Count} resized images for job {ResizeJobId}", 
                    resizedImages.Count, resizeJobId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting resized images for job {ResizeJobId}", resizeJobId);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(int imageId, int resizeJobId)
    {
        try
        {
            return await _context.ResizedImages
                .AnyAsync(ri => ri.ImageId == imageId && ri.ResizeJobId == resizeJobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if resized image exists for image {ImageId} and job {ResizeJobId}", 
                imageId, resizeJobId);
            throw;
        }
    }

    public async Task<int> GetCountByJobAsync(int resizeJobId, string? status = null)
    {
        try
        {
            var query = _context.ResizedImages.Where(ri => ri.ResizeJobId == resizeJobId);
            
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(ri => ri.Status == status);
            }

            return await query.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting count for job {ResizeJobId} with status {Status}", 
                resizeJobId, status);
            throw;
        }
    }

    public async Task UpdateStatusAsync(int id, string status, string? error = null)
    {
        try
        {
            var resizedImage = await _context.ResizedImages.FindAsync(id);
            if (resizedImage != null)
            {
                resizedImage.Status = status;
                resizedImage.ErrorMessage = error;
                
                if (status == "completed")
                {
                    resizedImage.CompletedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Updated status for resized image {Id} to {Status}", id, status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for resized image {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<ResizedImage>> GetOrphanedResizedImagesAsync()
    {
        try
        {
            return await _context.ResizedImages
                .Include(ri => ri.Image)
                .Include(ri => ri.ResizeJob)
                .Where(ri => ri.Image == null || ri.ResizeJob == null)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orphaned resized images");
            throw;
        }
    }
}