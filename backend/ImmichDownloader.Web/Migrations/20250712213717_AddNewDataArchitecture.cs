using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImmichDownloader.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddNewDataArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "albums",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    immich_id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    asset_count = table.Column<int>(type: "INTEGER", nullable: false),
                    is_shared = table.Column<bool>(type: "INTEGER", nullable: false),
                    owner_id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    owner_name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    thumbnail_asset_id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    last_synced = table.Column<DateTime>(type: "TEXT", nullable: true),
                    sync_status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    sync_error = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_albums", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "images",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    immich_id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    original_filename = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    file_path = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    file_size = table.Column<long>(type: "INTEGER", nullable: true),
                    file_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    width = table.Column<int>(type: "INTEGER", nullable: true),
                    height = table.Column<int>(type: "INTEGER", nullable: true),
                    checksum = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    is_downloaded = table.Column<bool>(type: "INTEGER", nullable: false),
                    download_attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    last_download_attempt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    downloaded_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_images", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "resize_jobs",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    job_id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    album_id = table.Column<int>(type: "INTEGER", nullable: true),
                    resize_profile_id = table.Column<int>(type: "INTEGER", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    total_images = table.Column<int>(type: "INTEGER", nullable: false),
                    processed_images = table.Column<int>(type: "INTEGER", nullable: false),
                    skipped_images = table.Column<int>(type: "INTEGER", nullable: false),
                    failed_images = table.Column<int>(type: "INTEGER", nullable: false),
                    output_zip_path = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    output_zip_size = table.Column<long>(type: "INTEGER", nullable: true),
                    error_message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    started_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    completed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resize_jobs", x => x.id);
                    table.ForeignKey(
                        name: "FK_resize_jobs_albums_album_id",
                        column: x => x.album_id,
                        principalTable: "albums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_resize_jobs_resize_profiles_resize_profile_id",
                        column: x => x.resize_profile_id,
                        principalTable: "resize_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "image_albums",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    image_id = table.Column<int>(type: "INTEGER", nullable: false),
                    album_id = table.Column<int>(type: "INTEGER", nullable: false),
                    position_in_album = table.Column<int>(type: "INTEGER", nullable: true),
                    added_to_album_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    removed_from_album_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_image_albums", x => x.id);
                    table.ForeignKey(
                        name: "FK_image_albums_albums_album_id",
                        column: x => x.album_id,
                        principalTable: "albums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_image_albums_images_image_id",
                        column: x => x.image_id,
                        principalTable: "images",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resized_images",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    image_id = table.Column<int>(type: "INTEGER", nullable: false),
                    resize_job_id = table.Column<int>(type: "INTEGER", nullable: false),
                    file_path = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    file_size = table.Column<long>(type: "INTEGER", nullable: false),
                    width = table.Column<int>(type: "INTEGER", nullable: false),
                    height = table.Column<int>(type: "INTEGER", nullable: false),
                    quality = table.Column<int>(type: "INTEGER", nullable: false),
                    format = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    processing_time_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    compression_ratio = table.Column<decimal>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    error_message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    completed_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resized_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_resized_images_images_image_id",
                        column: x => x.image_id,
                        principalTable: "images",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_resized_images_resize_jobs_resize_job_id",
                        column: x => x.resize_job_id,
                        principalTable: "resize_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_albums_immich_id",
                table: "albums",
                column: "immich_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_albums_is_active",
                table: "albums",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_albums_is_active_sync_status",
                table: "albums",
                columns: new[] { "is_active", "sync_status" });

            migrationBuilder.CreateIndex(
                name: "IX_albums_name",
                table: "albums",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_albums_sync_status",
                table: "albums",
                column: "sync_status");

            migrationBuilder.CreateIndex(
                name: "IX_image_albums_album_id",
                table: "image_albums",
                column: "album_id");

            migrationBuilder.CreateIndex(
                name: "IX_image_albums_album_id_is_active",
                table: "image_albums",
                columns: new[] { "album_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_image_albums_image_id_album_id",
                table: "image_albums",
                columns: new[] { "image_id", "album_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_image_albums_is_active",
                table: "image_albums",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_images_immich_id",
                table: "images",
                column: "immich_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_images_is_downloaded",
                table: "images",
                column: "is_downloaded");

            migrationBuilder.CreateIndex(
                name: "IX_images_is_downloaded_file_type",
                table: "images",
                columns: new[] { "is_downloaded", "file_type" });

            migrationBuilder.CreateIndex(
                name: "IX_images_original_filename",
                table: "images",
                column: "original_filename");

            migrationBuilder.CreateIndex(
                name: "IX_resize_jobs_album_id",
                table: "resize_jobs",
                column: "album_id");

            migrationBuilder.CreateIndex(
                name: "IX_resize_jobs_album_id_resize_profile_id",
                table: "resize_jobs",
                columns: new[] { "album_id", "resize_profile_id" });

            migrationBuilder.CreateIndex(
                name: "IX_resize_jobs_created_at",
                table: "resize_jobs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_resize_jobs_job_id",
                table: "resize_jobs",
                column: "job_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_resize_jobs_resize_profile_id",
                table: "resize_jobs",
                column: "resize_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_resize_jobs_status",
                table: "resize_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_resized_images_format",
                table: "resized_images",
                column: "format");

            migrationBuilder.CreateIndex(
                name: "IX_resized_images_image_id_resize_job_id",
                table: "resized_images",
                columns: new[] { "image_id", "resize_job_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_resized_images_resize_job_id",
                table: "resized_images",
                column: "resize_job_id");

            migrationBuilder.CreateIndex(
                name: "IX_resized_images_status",
                table: "resized_images",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_resized_images_status_format",
                table: "resized_images",
                columns: new[] { "status", "format" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "image_albums");

            migrationBuilder.DropTable(
                name: "resized_images");

            migrationBuilder.DropTable(
                name: "images");

            migrationBuilder.DropTable(
                name: "resize_jobs");

            migrationBuilder.DropTable(
                name: "albums");
        }
    }
}
