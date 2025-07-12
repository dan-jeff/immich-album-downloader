using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace ImmichDownloader.Web.Services.Repositories;

public class ImageRepository : IImageRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ImageRepository> _logger;

    public ImageRepository(ApplicationDbContext context, ILogger<ImageRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Image?> GetByIdAsync(int id)
    {
        try
        {
            return await _context.Images
                .Include(i => i.ImageAlbums)
                .ThenInclude(ia => ia.Album)
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving image by ID {ImageId}", id);
            throw;
        }
    }

    public async Task<Image?> GetByImmichIdAsync(string immichId)
    {
        try
        {
            return await _context.Images
                .Include(i => i.ImageAlbums)
                .ThenInclude(ia => ia.Album)
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.ImmichId == immichId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving image by Immich ID {ImmichId}", immichId);
            throw;
        }
    }

    public async Task<IEnumerable<Image>> GetByImmichIdsAsync(IEnumerable<string> immichIds)
    {
        try
        {
            var ids = immichIds.ToList();
            return await _context.Images
                .Where(i => ids.Contains(i.ImmichId))
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving images by Immich IDs");
            throw;
        }
    }

    public async Task<IEnumerable<Image>> GetDownloadedImagesAsync(int skip = 0, int take = 100)
    {
        try
        {
            return await _context.Images
                .Where(i => i.IsDownloaded)
                .OrderBy(i => i.DownloadedAt)
                .Skip(skip)
                .Take(take)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving downloaded images");
            throw;
        }
    }

    public async Task<IEnumerable<Image>> GetPendingDownloadsAsync(int skip = 0, int take = 100)
    {
        try
        {
            return await _context.Images
                .Where(i => !i.IsDownloaded)
                .OrderBy(i => i.CreatedAt)
                .Skip(skip)
                .Take(take)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending downloads");
            throw;
        }
    }

    public async Task<IEnumerable<Image>> GetImagesByAlbumIdAsync(int albumId, bool activeOnly = true)
    {
        try
        {
            var query = _context.Images
                .Include(i => i.ImageAlbums.Where(ia => ia.AlbumId == albumId))
                .Where(i => i.ImageAlbums.Any(ia => ia.AlbumId == albumId));

            if (activeOnly)
            {
                query = query.Where(i => i.ImageAlbums.Any(ia => ia.AlbumId == albumId && ia.IsActive));
            }

            return await query
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving images for album {AlbumId}", albumId);
            throw;
        }
    }

    public async Task<IEnumerable<Image>> GetDanglingImagesAsync()
    {
        try
        {
            return await _context.Images
                .Where(i => !i.ImageAlbums.Any(ia => ia.IsActive))
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dangling images");
            throw;
        }
    }

    public async Task<Image> CreateAsync(Image image)
    {
        try
        {
            image.CreatedAt = DateTime.UtcNow;
            image.UpdatedAt = DateTime.UtcNow;

            _context.Images.Add(image);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Created image with Immich ID {ImmichId}", image.ImmichId);
            return image;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating image with Immich ID {ImmichId}", image.ImmichId);
            throw;
        }
    }

    public async Task<Image> UpdateAsync(Image image)
    {
        try
        {
            image.UpdatedAt = DateTime.UtcNow;

            _context.Images.Update(image);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Updated image {ImageId}", image.Id);
            return image;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating image {ImageId}", image.Id);
            throw;
        }
    }

    public async Task DeleteAsync(int id)
    {
        try
        {
            var image = await _context.Images.FindAsync(id);
            if (image != null)
            {
                _context.Images.Remove(image);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Deleted image {ImageId}", id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image {ImageId}", id);
            throw;
        }
    }

    public async Task DeleteByImmichIdAsync(string immichId)
    {
        try
        {
            var image = await _context.Images.FirstOrDefaultAsync(i => i.ImmichId == immichId);
            if (image != null)
            {
                _context.Images.Remove(image);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Deleted image with Immich ID {ImmichId}", immichId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image with Immich ID {ImmichId}", immichId);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string immichId)
    {
        try
        {
            return await _context.Images.AnyAsync(i => i.ImmichId == immichId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if image exists with Immich ID {ImmichId}", immichId);
            throw;
        }
    }

    public async Task<int> GetTotalCountAsync()
    {
        try
        {
            return await _context.Images.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting total image count");
            throw;
        }
    }

    public async Task<int> GetDownloadedCountAsync()
    {
        try
        {
            return await _context.Images.CountAsync(i => i.IsDownloaded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting downloaded image count");
            throw;
        }
    }
}