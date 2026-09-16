using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Administration.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectApprovalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_projects_domain",
                table: "projects");

            migrationBuilder.AddColumn<string>(
                name: "api_domain",
                table: "projects",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "projects",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                table: "projects",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_approved_by",
                table: "projects",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "ix_projects_created_by",
                table: "projects",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_projects_domain",
                table: "projects",
                column: "domain",
                unique: true,
                filter: "state <> 'Rejected'");

            migrationBuilder.AddForeignKey(
                name: "fk_projects_users_approved_by",
                table: "projects",
                column: "approved_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_projects_users_created_by",
                table: "projects",
                column: "created_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_projects_users_approved_by",
                table: "projects");

            migrationBuilder.DropForeignKey(
                name: "fk_projects_users_created_by",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "ix_projects_approved_by",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "ix_projects_created_by",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "ix_projects_domain",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "api_domain",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "approved_by",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "description",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                table: "projects");

            migrationBuilder.CreateIndex(
                name: "ix_projects_domain",
                table: "projects",
                column: "domain",
                unique: true);
        }
    }
}
