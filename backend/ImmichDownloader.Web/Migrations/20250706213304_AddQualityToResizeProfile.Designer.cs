﻿// <auto-generated />
using System;
using ImmichDownloader.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace ImmichDownloader.Web.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20250706213304_AddQualityToResizeProfile")]
    partial class AddQualityToResizeProfile
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "9.0.0");

            modelBuilder.Entity("ImmichDownloader.Web.Models.AlbumChunk", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("AlbumId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("TEXT")
                        .HasColumnName("album_id");

                    b.Property<byte[]>("ChunkData")
                        .IsRequired()
                        .HasColumnType("BLOB")
                        .HasColumnName("chunk_data");

                    b.Property<int>("ChunkIndex")
                        .HasColumnType("INTEGER")
                        .HasColumnName("chunk_index");

                    b.Property<int>("ChunkSize")
                        .HasColumnType("INTEGER")
                        .HasColumnName("chunk_size");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("created_at");

                    b.Property<int>("DownloadedAlbumId")
                        .HasColumnType("INTEGER")
                        .HasColumnName("downloaded_album_id");

                    b.Property<int>("PhotoCount")
                        .HasColumnType("INTEGER")
                        .HasColumnName("photo_count");

                    b.HasKey("Id");

                    b.HasIndex("AlbumId");

                    b.HasIndex("DownloadedAlbumId", "ChunkIndex")
                        .IsUnique();

                    b.ToTable("album_chunks", (string)null);
                });

            modelBuilder.Entity("ImmichDownloader.Web.Models.AppSetting", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("created_at");

                    b.Property<string>("Key")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT")
                        .HasColumnName("key");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("updated_at");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("TEXT")
                        .HasColumnName("value");

                    b.HasKey("Id");

                    b.HasIndex("Key")
                        .IsUnique();

                    b.ToTable("app_settings", (string)null);
                });

            modelBuilder.Entity("ImmichDownloader.Web.Models.BackgroundTask", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(36)
                        .HasColumnType("TEXT");

                    b.Property<string>("AlbumId")
                        .HasMaxLength(36)
                        .HasColumnType("TEXT")
                        .HasColumnName("album_id");

                    b.Property<string>("AlbumName")
                        .HasMaxLength(255)
                        .HasColumnType("TEXT")
                        .HasColumnName("album_name");

                    b.Property<DateTime?>("CompletedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("completed_at");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("created_at");

                    b.Property<string>("CurrentStep")
                        .HasMaxLength(500)
                        .HasColumnType("TEXT")
                        .HasColumnName("current_step");

                    b.Property<int?>("DownloadedAlbumId")
                        .HasColumnType("INTEGER")
                        .HasColumnName("downloaded_album_id");

                    b.Property<string>("FilePath")
                        .HasMaxLength(500)
                        .HasColumnType("TEXT");

                    b.Property<long>("FileSize")
                        .HasColumnType("INTEGER");

                    b.Property<int>("ProcessedCount")
                        .HasColumnType("INTEGER")
                        .HasColumnName("processed_count");

                    b.Property<int?>("ProfileId")
                        .HasColumnType("INTEGER")
                        .HasColumnName("profile_id");

                    b.Property<int>("Progress")
                        .HasColumnType("INTEGER")
                        .HasColumnName("progress");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("status");

                    b.Property<string>("TaskType")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("task_type");

                    b.Property<int>("Total")
                        .HasColumnType("INTEGER")
                        .HasColumnName("total");

                    b.Property<byte[]>("ZipData")
                        .HasColumnType("BLOB")
                        .HasColumnName("zip_data");

                    b.Property<long>("ZipSize")
                        .HasColumnType("INTEGER")
                        .HasColumnName("zip_size");

                    b.HasKey("Id");

                    b.HasIndex("AlbumId");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("Status");

                    b.HasIndex("TaskType");

                    b.ToTable("background_tasks", (string)null);
                });

            modelBuilder.Entity("ImmichDownloader.Web.Models.DownloadedAlbum", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("AlbumId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("TEXT")
                        .HasColumnName("album_id");

                    b.Property<string>("AlbumName")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("TEXT")
                        .HasColumnName("album_name");

                    b.Property<int>("ChunkCount")
                        .HasColumnType("INTEGER")
                        .HasColumnName("chunk_count");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("created_at");

                    b.Property<string>("FilePath")
                        .HasMaxLength(500)
                        .HasColumnType("TEXT");

                    b.Property<string>("ImmichAlbumId")
                        .HasColumnType("TEXT");

                    b.Property<int>("PhotoCount")
                        .HasColumnType("INTEGER")
                        .HasColumnName("photo_count");

                    b.Property<long>("TotalSize")
                        .HasColumnType("INTEGER")
                        .HasColumnName("total_size");

                    b.HasKey("Id");

                    b.HasIndex("AlbumId");

                    b.HasIndex("ImmichAlbumId");

                    b.ToTable("downloaded_albums", (string)null);
                });

            modelBuilder.Entity("ImmichDownloader.Web.Models.DownloadedAsset", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("AlbumId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("TEXT")
                        .HasColumnName("album_id");

                    b.Property<string>("AssetId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("TEXT")
                        .HasColumnName("asset_id");

                    b.Property<int>("DownloadedAlbumId")
                        .HasColumnType("INTEGER")
                        .HasColumnName("downloaded_album_id");

                    b.Property<DateTime>("DownloadedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("downloaded_at");

                    b.Property<string>("FileName")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("TEXT")
                        .HasColumnName("file_name");

                    b.Property<long>("FileSize")
                        .HasColumnType("INTEGER")
                        .HasColumnName("file_size");

                    b.HasKey("Id");

                    b.HasIndex("AlbumId");

                    b.HasIndex("AssetId");

                    b.HasIndex("DownloadedAlbumId");

                    b.HasIndex("AssetId", "AlbumId")
                        .IsUnique();

                    b.ToTable("downloaded_assets", (string)null);
                });

            modelBuilder.Entity("ImmichDownloader.Web.Models.ImmichAlbum", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(36)
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("LastSynced")
                        .HasColumnType("TEXT")
                        .HasColumnName("last_synced");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("TEXT")
                        .HasColumnName("name");

                    b.Property<int>("PhotoCount")
                        .HasColumnType("INTEGER")
                        .HasColumnName("photo_count");

                    b.HasKey("Id");

                    b.HasIndex("Name");

                    b.ToTable("immich_albums", (string)null);
                });

            modelBuilder.Entity("ImmichDownloader.Web.Models.ResizeProfile", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("created_at");

                    b.Property<int>("Height")
                        .HasColumnType("INTEGER")
                        .HasColumnName("height");

                    b.Property<bool>("IncludeHorizontal")
                        .HasColumnType("INTEGER")
                        .HasColumnName("include_horizontal");

                    b.Property<bool>("IncludeVertical")
                        .HasColumnType("INTEGER")
                        .HasColumnName("include_vertical");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT")
                        .HasColumnName("name");

                    b.Property<int>("Quality")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Width")
                        .HasColumnType("INTEGER")
                        .HasColumnName("width");

                    b.HasKey("Id");

                    b.ToTable("resize_profiles", (string)null);
                });

            modelBuilder.Entity("ImmichDownloader.Web.Models.User", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("created_at");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("password_hash");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT")
                        .HasColumnName("username");

                    b.HasKey("Id");

                    b.HasIndex("Username")
                        .IsUnique();

                    b.ToTable("users", (string)null);
                });

            modelBuilder.Entity("ImmichDownloader.Web.Models.AlbumChunk", b =>
                {
                    b.HasOne("ImmichDownloader.Web.Models.DownloadedAlbum", "DownloadedAlbum")
                        .WithMany("Chunks")
                        .HasForeignKey("DownloadedAlbumId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("DownloadedAlbum");
                });

            modelBuilder.Entity("ImmichDownloader.Web.Models.DownloadedAlbum", b =>
                {
                    b.HasOne("ImmichDownloader.Web.Models.ImmichAlbum", "ImmichAlbum")
                        .WithMany()
                        .HasForeignKey("ImmichAlbumId");

                    b.Navigation("ImmichAlbum");
                });

            modelBuilder.Entity("ImmichDownloader.Web.Models.DownloadedAsset", b =>
                {
                    b.HasOne("ImmichDownloader.Web.Models.DownloadedAlbum", "DownloadedAlbum")
                        .WithMany()
                        .HasForeignKey("DownloadedAlbumId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("DownloadedAlbum");
                });

            modelBuilder.Entity("ImmichDownloader.Web.Models.DownloadedAlbum", b =>
                {
                    b.Navigation("Chunks");
                });
#pragma warning restore 612, 618
        }
    }
}
