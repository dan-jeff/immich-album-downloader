using Microsoft.EntityFrameworkCore;
using ImmichDownloader.Web.Models;

namespace ImmichDownloader.Web.Data;

/// <summary>
/// Entity Framework Core database context for the Immich Downloader application.
/// Manages all database operations and entity relationships using SQLite as the data store.
/// Configured to use snake_case naming convention for compatibility with the original Python schema.
/// </summary>
public class ApplicationDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the ApplicationDbContext with the specified options.
    /// </summary>
    /// <param name="options">The options to configure the database context.</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the DbSet for User entities.
    /// Represents the users table containing authentication information.
    /// </summary>
    public DbSet<User> Users { get; set; }
    
    /// <summary>
    /// Gets or sets the DbSet for ResizeProfile entities.
    /// Represents the resize_profiles table containing image processing configurations.
    /// </summary>
    public DbSet<ResizeProfile> ResizeProfiles { get; set; }
    
    /// <summary>
    /// Gets or sets the DbSet for ImmichAlbum entities.
    /// Represents the immich_albums table containing cached album information from the Immich server.
    /// </summary>
    public DbSet<ImmichAlbum> ImmichAlbums { get; set; }
    
    /// <summary>
    /// Gets or sets the DbSet for DownloadedAlbum entities.
    /// Represents the downloaded_albums table containing metadata for successfully downloaded albums.
    /// </summary>
    public DbSet<DownloadedAlbum> DownloadedAlbums { get; set; }
    
    /// <summary>
    /// Gets or sets the DbSet for AlbumChunk entities.
    /// Represents the album_chunks table containing chunked album data for legacy storage mode.
    /// </summary>
    public DbSet<AlbumChunk> AlbumChunks { get; set; }
    
    /// <summary>
    /// Gets or sets the DbSet for DownloadedAsset entities.
    /// Represents the downloaded_assets table containing metadata for individual downloaded files.
    /// </summary>
    public DbSet<DownloadedAsset> DownloadedAssets { get; set; }
    
    /// <summary>
    /// Gets or sets the DbSet for BackgroundTask entities.
    /// Represents the background_tasks table containing information about download and resize operations.
    /// </summary>
    public DbSet<BackgroundTask> BackgroundTasks { get; set; }
    
    /// <summary>
    /// Gets or sets the DbSet for AppSetting entities.
    /// Represents the app_settings table containing application configuration key-value pairs.
    /// </summary>
    public DbSet<AppSetting> AppSettings { get; set; }
    
    /// <summary>
    /// Gets or sets the DbSet for Image entities.
    /// Represents the images table containing individual image metadata with Immich IDs.
    /// </summary>
    public DbSet<Image> Images { get; set; }
    
    /// <summary>
    /// Gets or sets the DbSet for Album entities.
    /// Represents the albums table containing enhanced album information from Immich.
    /// </summary>
    public DbSet<Album> Albums { get; set; }
    
    /// <summary>
    /// Gets or sets the DbSet for ImageAlbum entities.
    /// Represents the image_albums table providing many-to-many relationships between images and albums.
    /// </summary>
    public DbSet<ImageAlbum> ImageAlbums { get; set; }
    
    /// <summary>
    /// Gets or sets the DbSet for ResizeJob entities.
    /// Represents the resize_jobs table containing resize operation tracking and configuration.
    /// </summary>
    public DbSet<ResizeJob> ResizeJobs { get; set; }
    
    /// <summary>
    /// Gets or sets the DbSet for ResizedImage entities.
    /// Represents the resized_images table containing metadata for processed resize operations.
    /// </summary>
    public DbSet<ResizedImage> ResizedImages { get; set; }

    /// <summary>
    /// Configures the model relationships, constraints, and database mappings.
    /// Sets up snake_case table and column names for compatibility with the original Python schema.
    /// Defines foreign key relationships, indexes, and unique constraints for optimal performance.
    /// </summary>
    /// <param name="modelBuilder">The model builder used to configure the entities.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure table names to match Python schema
        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<ResizeProfile>().ToTable("resize_profiles");
        modelBuilder.Entity<ImmichAlbum>().ToTable("immich_albums");
        modelBuilder.Entity<DownloadedAlbum>().ToTable("downloaded_albums");
        modelBuilder.Entity<AlbumChunk>().ToTable("album_chunks");
        modelBuilder.Entity<BackgroundTask>().ToTable("background_tasks");
        modelBuilder.Entity<AppSetting>().ToTable("app_settings");
        
        // Configure new entity table names
        modelBuilder.Entity<Image>().ToTable("images");
        modelBuilder.Entity<Album>().ToTable("albums");
        modelBuilder.Entity<ImageAlbum>().ToTable("image_albums");
        modelBuilder.Entity<ResizeJob>().ToTable("resize_jobs");
        modelBuilder.Entity<ResizedImage>().ToTable("resized_images");

        // Configure User entity with snake_case column names and constraints
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.Username).HasColumnName("username");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            
            // Add unique index on username for faster lookups and authentication
            entity.HasIndex(e => e.Username).IsUnique();
        });

        // Configure ResizeProfile entity with snake_case column names
        modelBuilder.Entity<ResizeProfile>(entity =>
        {
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Width).HasColumnName("width");
            entity.Property(e => e.Height).HasColumnName("height");
            entity.Property(e => e.IncludeHorizontal).HasColumnName("include_horizontal");
            entity.Property(e => e.IncludeVertical).HasColumnName("include_vertical");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        // Configure ImmichAlbum entity with snake_case column names and search index
        modelBuilder.Entity<ImmichAlbum>(entity =>
        {
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.PhotoCount).HasColumnName("photo_count");
            entity.Property(e => e.LastSynced).HasColumnName("last_synced");
            
            // Add index on album name for faster searches and filtering
            entity.HasIndex(e => e.Name);
        });

        // Configure DownloadedAlbum entity with snake_case column names and lookup index
        modelBuilder.Entity<DownloadedAlbum>(entity =>
        {
            entity.Property(e => e.AlbumId).HasColumnName("album_id");
            entity.Property(e => e.AlbumName).HasColumnName("album_name");
            entity.Property(e => e.PhotoCount).HasColumnName("photo_count");
            entity.Property(e => e.TotalSize).HasColumnName("total_size");
            entity.Property(e => e.ChunkCount).HasColumnName("chunk_count");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            
            // Add index on album_id for faster lookups and relationship queries
            entity.HasIndex(e => e.AlbumId);
        });

        // Configure AlbumChunk entity with foreign key relationships and constraints
        modelBuilder.Entity<AlbumChunk>(entity =>
        {
            entity.Property(e => e.AlbumId).HasColumnName("album_id");
            entity.Property(e => e.DownloadedAlbumId).HasColumnName("downloaded_album_id");
            entity.Property(e => e.ChunkIndex).HasColumnName("chunk_index");
            entity.Property(e => e.ChunkData).HasColumnName("chunk_data");
            entity.Property(e => e.ChunkSize).HasColumnName("chunk_size");
            entity.Property(e => e.PhotoCount).HasColumnName("photo_count");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            
            // Configure foreign key relationship to DownloadedAlbum with cascade delete
            entity.HasOne(e => e.DownloadedAlbum)
                  .WithMany(a => a.Chunks)
                  .HasForeignKey(e => e.DownloadedAlbumId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            // Unique constraint for downloaded_album_id + chunk_index to ensure proper ordering
            entity.HasIndex(e => new { e.DownloadedAlbumId, e.ChunkIndex }).IsUnique();
            // Additional index on album_id for faster album-based queries
            entity.HasIndex(e => e.AlbumId);
        });

        // Configure DownloadedAsset entity with foreign key relationships and performance indexes
        modelBuilder.Entity<DownloadedAsset>(entity =>
        {
            entity.ToTable("downloaded_assets");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.AlbumId).HasColumnName("album_id");
            entity.Property(e => e.FileName).HasColumnName("file_name");
            entity.Property(e => e.FileSize).HasColumnName("file_size");
            entity.Property(e => e.DownloadedAt).HasColumnName("downloaded_at");
            entity.Property(e => e.DownloadedAlbumId).HasColumnName("downloaded_album_id");
            
            // Configure foreign key relationship to DownloadedAlbum with cascade delete
            entity.HasOne(e => e.DownloadedAlbum)
                  .WithMany()
                  .HasForeignKey(e => e.DownloadedAlbumId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            // Unique constraint for asset_id + album_id to prevent duplicate downloads
            entity.HasIndex(e => new { e.AssetId, e.AlbumId }).IsUnique();
            // Additional indexes for common query patterns
            entity.HasIndex(e => e.AssetId);
            entity.HasIndex(e => e.AlbumId);
        });

        // Configure BackgroundTask entity with enum storage and performance indexes
        modelBuilder.Entity<BackgroundTask>(entity =>
        {
            entity.Property(e => e.TaskType).HasColumnName("task_type");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Progress).HasColumnName("progress");
            entity.Property(e => e.Total).HasColumnName("total");
            entity.Property(e => e.CurrentStep).HasColumnName("current_step");
            entity.Property(e => e.AlbumId).HasColumnName("album_id");
            entity.Property(e => e.AlbumName).HasColumnName("album_name");
            entity.Property(e => e.DownloadedAlbumId).HasColumnName("downloaded_album_id");
            entity.Property(e => e.ProfileId).HasColumnName("profile_id");
            entity.Property(e => e.ZipData).HasColumnName("zip_data");
            entity.Property(e => e.ZipSize).HasColumnName("zip_size");
            entity.Property(e => e.ProcessedCount).HasColumnName("processed_count");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");

            // Configure enum storage as strings for better readability in database
            entity.Property(e => e.TaskType).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
            
            // Add indexes for commonly queried fields to improve performance
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.TaskType);
            entity.HasIndex(e => e.AlbumId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure AppSetting entity with unique key constraint
        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.Property(e => e.Key).HasColumnName("key");
            entity.Property(e => e.Value).HasColumnName("value");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            
            // Unique constraint on key to ensure one value per configuration setting
            entity.HasIndex(e => e.Key).IsUnique();
        });

        // Configure Image entity with proper indexes and constraints
        modelBuilder.Entity<Image>(entity =>
        {
            // Unique index on immich_id to prevent duplicate images from Immich
            entity.HasIndex(e => e.ImmichId).IsUnique();
            
            // Index on is_downloaded for filtering downloaded vs pending images
            entity.HasIndex(e => e.IsDownloaded);
            
            // Composite index for download status and file type queries
            entity.HasIndex(e => new { e.IsDownloaded, e.FileType });
            
            // Index on original filename for search functionality
            entity.HasIndex(e => e.OriginalFilename);
        });

        // Configure Album entity with performance indexes
        modelBuilder.Entity<Album>(entity =>
        {
            // Unique index on immich_id to prevent duplicate albums from Immich
            entity.HasIndex(e => e.ImmichId).IsUnique();
            
            // Index on name for search and filtering
            entity.HasIndex(e => e.Name);
            
            // Index on sync_status for filtering sync operations
            entity.HasIndex(e => e.SyncStatus);
            
            // Index on is_active for filtering active albums
            entity.HasIndex(e => e.IsActive);
            
            // Composite index for active albums with sync status
            entity.HasIndex(e => new { e.IsActive, e.SyncStatus });
        });

        // Configure ImageAlbum junction table with proper relationships and constraints
        modelBuilder.Entity<ImageAlbum>(entity =>
        {
            // Configure foreign key relationships
            entity.HasOne(e => e.Image)
                  .WithMany(i => i.ImageAlbums)
                  .HasForeignKey(e => e.ImageId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Album)
                  .WithMany(a => a.ImageAlbums)
                  .HasForeignKey(e => e.AlbumId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            // Unique constraint to prevent duplicate image-album relationships
            entity.HasIndex(e => new { e.ImageId, e.AlbumId }).IsUnique();
            
            // Index on album_id for querying images in an album
            entity.HasIndex(e => e.AlbumId);
            
            // Index on is_active for filtering active relationships
            entity.HasIndex(e => e.IsActive);
            
            // Composite index for active relationships in albums
            entity.HasIndex(e => new { e.AlbumId, e.IsActive });
        });

        // Configure ResizeJob entity with relationships and performance indexes
        modelBuilder.Entity<ResizeJob>(entity =>
        {
            // Configure foreign key relationships
            entity.HasOne(e => e.Album)
                  .WithMany(a => a.ResizeJobs)
                  .HasForeignKey(e => e.AlbumId)
                  .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.ResizeProfile)
                  .WithMany()
                  .HasForeignKey(e => e.ResizeProfileId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            // Unique index on job_id for external references
            entity.HasIndex(e => e.JobId).IsUnique();
            
            // Index on status for filtering jobs by status
            entity.HasIndex(e => e.Status);
            
            // Index on album_id for album-specific resize jobs
            entity.HasIndex(e => e.AlbumId);
            
            // Index on resize_profile_id for profile-specific queries
            entity.HasIndex(e => e.ResizeProfileId);
            
            // Composite index for album and profile combinations
            entity.HasIndex(e => new { e.AlbumId, e.ResizeProfileId });
            
            // Index on created_at for sorting and time-based queries
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure ResizedImage entity with relationships and indexes
        modelBuilder.Entity<ResizedImage>(entity =>
        {
            // Configure foreign key relationships
            entity.HasOne(e => e.Image)
                  .WithMany(i => i.ResizedImages)
                  .HasForeignKey(e => e.ImageId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.ResizeJob)
                  .WithMany(j => j.ResizedImages)
                  .HasForeignKey(e => e.ResizeJobId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            // Unique constraint to prevent duplicate resized images for same job
            entity.HasIndex(e => new { e.ImageId, e.ResizeJobId }).IsUnique();
            
            // Index on resize_job_id for job-specific queries
            entity.HasIndex(e => e.ResizeJobId);
            
            // Index on status for filtering by processing status
            entity.HasIndex(e => e.Status);
            
            // Index on format for filtering by output format
            entity.HasIndex(e => e.Format);
            
            // Composite index for completed resized images
            entity.HasIndex(e => new { e.Status, e.Format });
        });
    }
}