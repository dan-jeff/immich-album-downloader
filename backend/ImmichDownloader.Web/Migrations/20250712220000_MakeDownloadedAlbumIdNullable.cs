using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImmichDownloader.Web.Migrations
{
    /// <inheritdoc />
    public partial class MakeDownloadedAlbumIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update the downloaded_album_id column to allow NULL values
            migrationBuilder.AlterColumn<int>(
                name: "downloaded_album_id",
                table: "downloaded_assets",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert back to NOT NULL (this might fail if there are NULL values)
            migrationBuilder.AlterColumn<int>(
                name: "downloaded_album_id",
                table: "downloaded_assets",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}