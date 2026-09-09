using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordingPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recording_photos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    recording_id = table.Column<int>(type: "integer", nullable: false),
                    file_path = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("recording_photos_pkey", x => x.id);
                    table.ForeignKey(
                        name: "recording_photos_recording_id_fkey",
                        column: x => x.recording_id,
                        principalTable: "recordings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recording_photos_recording_id",
                table: "recording_photos",
                column: "recording_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recording_photos");
        }
    }
}
