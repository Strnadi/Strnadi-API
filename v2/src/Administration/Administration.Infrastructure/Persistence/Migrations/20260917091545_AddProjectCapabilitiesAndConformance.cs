using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Administration.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectCapabilitiesAndConformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "api_conformance_checked_at",
                table: "projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "api_conformance_report",
                table: "projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "capabilities_checked_at",
                table: "projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "capabilities_json",
                table: "projects",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "api_conformance_checked_at",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "api_conformance_report",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "capabilities_checked_at",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "capabilities_json",
                table: "projects");
        }
    }
}
