using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Administration.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_document_acceptances_user_id_document_id",
                table: "document_acceptances");

            migrationBuilder.AddColumn<string>(
                name: "preferred_language",
                table: "users",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "effective_at",
                table: "documents",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "revoked_at",
                table: "document_acceptances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_acceptances_user_id_document_id",
                table: "document_acceptances",
                columns: new[] { "user_id", "document_id" },
                unique: true,
                filter: "revoked_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_document_acceptances_user_id_document_id",
                table: "document_acceptances");

            migrationBuilder.DropColumn(
                name: "preferred_language",
                table: "users");

            migrationBuilder.DropColumn(
                name: "effective_at",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "revoked_at",
                table: "document_acceptances");

            migrationBuilder.CreateIndex(
                name: "ix_document_acceptances_user_id_document_id",
                table: "document_acceptances",
                columns: new[] { "user_id", "document_id" },
                unique: true);
        }
    }
}
