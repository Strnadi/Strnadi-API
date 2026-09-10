using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Administration.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectPhotoPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "photo_format",
                table: "projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "photo_path",
                table: "projects",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "photo_format",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "photo_path",
                table: "projects");
        }
    }
}
