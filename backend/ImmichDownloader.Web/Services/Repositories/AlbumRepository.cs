using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace ImmichDownloader.Web.Services.Repositories;

public class AlbumRepository : IAlbumRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AlbumRepository> _logger;

    public AlbumRepository(ApplicationDbContext context, ILogger<AlbumRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Album?> GetByIdAsync(int id)
    {
        try
        {
            return await _context.Albums
                .Include(a => a.ImageAlbums)
                .ThenInclude(ia => ia.Image)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving album by ID {AlbumId}", id);
            throw;
        }
    }

    public async Task<Album?> GetByImmichIdAsync(string immichId)
    {
        try
        {
            return await _context.Albums
                .Include(a => a.ImageAlbums)
                .ThenInclude(ia => ia.Image)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ImmichId == immichId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving album by Immich ID {ImmichId}", immichId);
            throw;
        }
    }

    public async Task<IEnumerable<Album>> GetActiveAlbumsAsync()
    {
        try
        {
            return await _context.Albums
                .Where(a => a.IsActive)
                .OrderBy(a => a.Name)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active albums");
            throw;
        }
    }

    public async Task<IEnumerable<Album>> GetAlbumsByStatusAsync(string syncStatus)
    {
        try
        {
            return await _context.Albums
                .Where(a => a.SyncStatus == syncStatus && a.IsActive)
                .OrderBy(a => a.UpdatedAt)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving albums by status {SyncStatus}", syncStatus);
            throw;
        }
    }

    public async Task<IEnumerable<Album>> GetAlbumsNeedingSyncAsync()
    {
        try
        {
            var staleThreshold = DateTime.UtcNow.AddHours(-1);
            
            return await _context.Albums
                .Where(a => a.IsActive && 
                           (a.SyncStatus == "pending" || 
                            (a.SyncStatus == "error" && a.UpdatedAt < staleThreshold) ||
                            a.LastSynced == null ||
                            a.LastSynced < DateTime.UtcNow.AddDays(-1)))
                .OrderBy(a => a.LastSynced ?? DateTime.MinValue)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving albums needing sync");
            throw;
        }
    }

    public async Task<Album> CreateAsync(Album album)
    {
        try
        {
            album.CreatedAt = DateTime.UtcNow;
            album.UpdatedAt = DateTime.UtcNow;

            _context.Albums.Add(album);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Created album {AlbumName} with Immich ID {ImmichId}", album.Name, album.ImmichId);
            return album;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating album {AlbumName}", album.Name);
            throw;
        }
    }

    public async Task<Album> UpdateAsync(Album album)
    {
        try
        {
            album.UpdatedAt = DateTime.UtcNow;

            _context.Albums.Update(album);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Updated album {AlbumId}", album.Id);
            return album;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating album {AlbumId}", album.Id);
            throw;
        }
    }

    public async Task DeleteAsync(int id)
    {
        try
        {
            var album = await _context.Albums.FindAsync(id);
            if (album != null)
            {
                _context.Albums.Remove(album);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Deleted album {AlbumId}", id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting album {AlbumId}", id);
            throw;
        }
    }

    public async Task DeactivateAsync(int id)
    {
        try
        {
            var album = await _context.Albums.FindAsync(id);
            if (album != null)
            {
                album.IsActive = false;
                album.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogDebug("Deactivated album {AlbumId}", id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating album {AlbumId}", id);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string immichId)
    {
        try
        {
            return await _context.Albums.AnyAsync(a => a.ImmichId == immichId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if album exists with Immich ID {ImmichId}", immichId);
            throw;
        }
    }

    public async Task<int> GetTotalCountAsync()
    {
        try
        {
            return await _context.Albums.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting total album count");
            throw;
        }
    }

    public async Task<int> GetActiveCountAsync()
    {
        try
        {
            return await _context.Albums.CountAsync(a => a.IsActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active album count");
            throw;
        }
    }

    public async Task UpdateSyncStatusAsync(int id, string status, string? error = null)
    {
        try
        {
            var album = await _context.Albums.FindAsync(id);
            if (album != null)
            {
                album.SyncStatus = status;
                album.SyncError = error;
                album.UpdatedAt = DateTime.UtcNow;
                
                if (status == "completed")
                {
                    album.LastSynced = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Updated sync status for album {AlbumId} to {Status}", id, status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating sync status for album {AlbumId}", id);
            throw;
        }
    }
}