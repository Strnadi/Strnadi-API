using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDialect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_dialect",
                table: "dialects",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_dialect",
                table: "dialects");
        }
    }
}
