using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordingUploadConfirmed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "upload_confirmed",
                table: "recordings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "upload_confirmed",
                table: "recordings");
        }
    }
}
