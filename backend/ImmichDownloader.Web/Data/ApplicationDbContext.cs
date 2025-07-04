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
    }
}