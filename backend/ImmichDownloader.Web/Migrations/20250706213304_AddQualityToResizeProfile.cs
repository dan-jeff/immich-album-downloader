using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImmichDownloader.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityToResizeProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Quality",
                table: "resize_profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Quality",
                table: "resize_profiles");
        }
    }
}
