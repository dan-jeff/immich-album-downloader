using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace ImmichDownloader.Web.Services.Repositories;

public class ImageAlbumRepository : IImageAlbumRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ImageAlbumRepository> _logger;

    public ImageAlbumRepository(ApplicationDbContext context, ILogger<ImageAlbumRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ImageAlbum?> GetByIdAsync(int id)
    {
        try
        {
            return await _context.ImageAlbums
                .Include(ia => ia.Image)
                .Include(ia => ia.Album)
                .AsNoTracking()
                .FirstOrDefaultAsync(ia => ia.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving image-album relationship by ID {Id}", id);
            throw;
        }
    }

    public async Task<ImageAlbum?> GetByImageAndAlbumAsync(int imageId, int albumId)
    {
        try
        {
            return await _context.ImageAlbums
                .Include(ia => ia.Image)
                .Include(ia => ia.Album)
                .AsNoTracking()
                .FirstOrDefaultAsync(ia => ia.ImageId == imageId && ia.AlbumId == albumId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving image-album relationship for image {ImageId} and album {AlbumId}", imageId, albumId);
            throw;
        }
    }

    public async Task<IEnumerable<ImageAlbum>> GetByAlbumIdAsync(int albumId, bool activeOnly = true)
    {
        try
        {
            var query = _context.ImageAlbums
                .Include(ia => ia.Image)
                .Where(ia => ia.AlbumId == albumId);

            if (activeOnly)
            {
                query = query.Where(ia => ia.IsActive);
            }

            return await query
                .OrderBy(ia => ia.PositionInAlbum.HasValue ? ia.PositionInAlbum.Value : 0)
                .ThenBy(ia => ia.AddedToAlbumAt)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving image-album relationships for album {AlbumId}", albumId);
            throw;
        }
    }

    public async Task<IEnumerable<ImageAlbum>> GetByImageIdAsync(int imageId, bool activeOnly = true)
    {
        try
        {
            var query = _context.ImageAlbums
                .Include(ia => ia.Album)
                .Where(ia => ia.ImageId == imageId);

            if (activeOnly)
            {
                query = query.Where(ia => ia.IsActive);
            }

            return await query
                .OrderBy(ia => ia.AddedToAlbumAt)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving image-album relationships for image {ImageId}", imageId);
            throw;
        }
    }

    public async Task<ImageAlbum> CreateAsync(ImageAlbum imageAlbum)
    {
        try
        {
            imageAlbum.AddedToAlbumAt = DateTime.UtcNow;
            imageAlbum.CreatedAt = DateTime.UtcNow;
            imageAlbum.IsActive = true;

            _context.ImageAlbums.Add(imageAlbum);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Created image-album relationship for image {ImageId} and album {AlbumId}", 
                imageAlbum.ImageId, imageAlbum.AlbumId);
            return imageAlbum;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating image-album relationship for image {ImageId} and album {AlbumId}", 
                imageAlbum.ImageId, imageAlbum.AlbumId);
            throw;
        }
    }

    public async Task<ImageAlbum> UpdateAsync(ImageAlbum imageAlbum)
    {
        try
        {
            _context.ImageAlbums.Update(imageAlbum);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Updated image-album relationship {Id}", imageAlbum.Id);
            return imageAlbum;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating image-album relationship {Id}", imageAlbum.Id);
            throw;
        }
    }

    public async Task DeleteAsync(int id)
    {
        try
        {
            var imageAlbum = await _context.ImageAlbums.FindAsync(id);
            if (imageAlbum != null)
            {
                _context.ImageAlbums.Remove(imageAlbum);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Deleted image-album relationship {Id}", id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image-album relationship {Id}", id);
            throw;
        }
    }

    public async Task DeactivateAsync(int imageId, int albumId)
    {
        try
        {
            var imageAlbum = await _context.ImageAlbums
                .FirstOrDefaultAsync(ia => ia.ImageId == imageId && ia.AlbumId == albumId);
            
            if (imageAlbum != null)
            {
                imageAlbum.IsActive = false;
                imageAlbum.RemovedFromAlbumAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                
                _logger.LogDebug("Deactivated image-album relationship for image {ImageId} and album {AlbumId}", 
                    imageId, albumId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating image-album relationship for image {ImageId} and album {AlbumId}", 
                imageId, albumId);
            throw;
        }
    }

    public async Task ActivateAsync(int imageId, int albumId)
    {
        try
        {
            var imageAlbum = await _context.ImageAlbums
                .FirstOrDefaultAsync(ia => ia.ImageId == imageId && ia.AlbumId == albumId);
            
            if (imageAlbum != null)
            {
                imageAlbum.IsActive = true;
                imageAlbum.RemovedFromAlbumAt = null;
                imageAlbum.AddedToAlbumAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                
                _logger.LogDebug("Activated image-album relationship for image {ImageId} and album {AlbumId}", 
                    imageId, albumId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating image-album relationship for image {ImageId} and album {AlbumId}", 
                imageId, albumId);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(int imageId, int albumId)
    {
        try
        {
            return await _context.ImageAlbums
                .AnyAsync(ia => ia.ImageId == imageId && ia.AlbumId == albumId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if image-album relationship exists for image {ImageId} and album {AlbumId}", 
                imageId, albumId);
            throw;
        }
    }

    public async Task<int> GetCountByAlbumAsync(int albumId, bool activeOnly = true)
    {
        try
        {
            var query = _context.ImageAlbums.Where(ia => ia.AlbumId == albumId);
            
            if (activeOnly)
            {
                query = query.Where(ia => ia.IsActive);
            }

            return await query.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting count for album {AlbumId}", albumId);
            throw;
        }
    }

    public async Task RemoveImageFromAlbumAsync(int imageId, int albumId)
    {
        try
        {
            var imageAlbum = await _context.ImageAlbums
                .FirstOrDefaultAsync(ia => ia.ImageId == imageId && ia.AlbumId == albumId);
            
            if (imageAlbum != null)
            {
                _context.ImageAlbums.Remove(imageAlbum);
                await _context.SaveChangesAsync();
                
                _logger.LogDebug("Removed image {ImageId} from album {AlbumId}", imageId, albumId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing image {ImageId} from album {AlbumId}", imageId, albumId);
            throw;
        }
    }

    public async Task<IEnumerable<ImageAlbum>> GetInactiveRelationshipsAsync()
    {
        try
        {
            return await _context.ImageAlbums
                .Include(ia => ia.Image)
                .Include(ia => ia.Album)
                .Where(ia => !ia.IsActive)
                .OrderBy(ia => ia.RemovedFromAlbumAt)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inactive image-album relationships");
            throw;
        }
    }
}