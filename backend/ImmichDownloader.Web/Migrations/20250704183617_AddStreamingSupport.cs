using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImmichDownloader.Web.Migrations;

/// <inheritdoc />
public partial class AddStreamingSupport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "app_settings",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_app_settings", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "background_tasks",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                task_type = table.Column<string>(type: "TEXT", nullable: false),
                status = table.Column<string>(type: "TEXT", nullable: false),
                progress = table.Column<int>(type: "INTEGER", nullable: false),
                total = table.Column<int>(type: "INTEGER", nullable: false),
                current_step = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                album_id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                album_name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                downloaded_album_id = table.Column<int>(type: "INTEGER", nullable: true),
                profile_id = table.Column<int>(type: "INTEGER", nullable: true),
                zip_data = table.Column<byte[]>(type: "BLOB", nullable: true),
                FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                zip_size = table.Column<long>(type: "INTEGER", nullable: false),
                FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                processed_count = table.Column<int>(type: "INTEGER", nullable: false),
                created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                completed_at = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_background_tasks", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "immich_albums",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                photo_count = table.Column<int>(type: "INTEGER", nullable: false),
                last_synced = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_immich_albums", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "resize_profiles",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                width = table.Column<int>(type: "INTEGER", nullable: false),
                height = table.Column<int>(type: "INTEGER", nullable: false),
                include_horizontal = table.Column<bool>(type: "INTEGER", nullable: false),
                include_vertical = table.Column<bool>(type: "INTEGER", nullable: false),
                created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_resize_profiles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                password_hash = table.Column<string>(type: "TEXT", nullable: false),
                created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "downloaded_albums",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                album_id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                album_name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                photo_count = table.Column<int>(type: "INTEGER", nullable: false),
                total_size = table.Column<long>(type: "INTEGER", nullable: false),
                chunk_count = table.Column<int>(type: "INTEGER", nullable: false),
                created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                ImmichAlbumId = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_downloaded_albums", x => x.Id);
                table.ForeignKey(
                    name: "FK_downloaded_albums_immich_albums_ImmichAlbumId",
                    column: x => x.ImmichAlbumId,
                    principalTable: "immich_albums",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "album_chunks",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                album_id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                downloaded_album_id = table.Column<int>(type: "INTEGER", nullable: false),
                chunk_index = table.Column<int>(type: "INTEGER", nullable: false),
                chunk_data = table.Column<byte[]>(type: "BLOB", nullable: false),
                chunk_size = table.Column<int>(type: "INTEGER", nullable: false),
                photo_count = table.Column<int>(type: "INTEGER", nullable: false),
                created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_album_chunks", x => x.Id);
                table.ForeignKey(
                    name: "FK_album_chunks_downloaded_albums_downloaded_album_id",
                    column: x => x.downloaded_album_id,
                    principalTable: "downloaded_albums",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "downloaded_assets",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                asset_id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                album_id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                file_name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                file_size = table.Column<long>(type: "INTEGER", nullable: false),
                downloaded_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                downloaded_album_id = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_downloaded_assets", x => x.Id);
                table.ForeignKey(
                    name: "FK_downloaded_assets_downloaded_albums_downloaded_album_id",
                    column: x => x.downloaded_album_id,
                    principalTable: "downloaded_albums",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_album_chunks_album_id",
            table: "album_chunks",
            column: "album_id");

        migrationBuilder.CreateIndex(
            name: "IX_album_chunks_downloaded_album_id_chunk_index",
            table: "album_chunks",
            columns: new[] { "downloaded_album_id", "chunk_index" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_app_settings_key",
            table: "app_settings",
            column: "key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_background_tasks_album_id",
            table: "background_tasks",
            column: "album_id");

        migrationBuilder.CreateIndex(
            name: "IX_background_tasks_created_at",
            table: "background_tasks",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "IX_background_tasks_status",
            table: "background_tasks",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "IX_background_tasks_task_type",
            table: "background_tasks",
            column: "task_type");

        migrationBuilder.CreateIndex(
            name: "IX_downloaded_albums_album_id",
            table: "downloaded_albums",
            column: "album_id");

        migrationBuilder.CreateIndex(
            name: "IX_downloaded_albums_ImmichAlbumId",
            table: "downloaded_albums",
            column: "ImmichAlbumId");

        migrationBuilder.CreateIndex(
            name: "IX_downloaded_assets_album_id",
            table: "downloaded_assets",
            column: "album_id");

        migrationBuilder.CreateIndex(
            name: "IX_downloaded_assets_asset_id",
            table: "downloaded_assets",
            column: "asset_id");

        migrationBuilder.CreateIndex(
            name: "IX_downloaded_assets_asset_id_album_id",
            table: "downloaded_assets",
            columns: new[] { "asset_id", "album_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_downloaded_assets_downloaded_album_id",
            table: "downloaded_assets",
            column: "downloaded_album_id");

        migrationBuilder.CreateIndex(
            name: "IX_immich_albums_name",
            table: "immich_albums",
            column: "name");

        migrationBuilder.CreateIndex(
            name: "IX_users_username",
            table: "users",
            column: "username",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "album_chunks");

        migrationBuilder.DropTable(
            name: "app_settings");

        migrationBuilder.DropTable(
            name: "background_tasks");

        migrationBuilder.DropTable(
            name: "downloaded_assets");

        migrationBuilder.DropTable(
            name: "resize_profiles");

        migrationBuilder.DropTable(
            name: "users");

        migrationBuilder.DropTable(
            name: "downloaded_albums");

        migrationBuilder.DropTable(
            name: "immich_albums");
    }
}
