using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.CreateTable(
                name: "achievements",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    image_path = table.Column<string>(type: "text", nullable: true),
                    sql = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("achievements_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "article_categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    label = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    order = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("article_categories_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "articles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("articles_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fcm_token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    device_platform = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    device_model = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("devices_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dialects",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    dialect_code = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false, defaultValueSql: "''::character varying"),
                    hint_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("dialects_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recordings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    estimated_birds_count = table.Column<short>(type: "smallint", nullable: true),
                    by_app = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    note_post = table.Column<string>(type: "text", nullable: true),
                    device = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    legacy = table.Column<bool>(type: "boolean", nullable: false),
                    expected_parts_count = table.Column<int>(type: "integer", nullable: true),
                    upload_confirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("recordings_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "achievement_content",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    achievement_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("achievement_content_pkey", x => x.id);
                    table.ForeignKey(
                        name: "achievement_content_achievement_id_fkey",
                        column: x => x.achievement_id,
                        principalTable: "achievements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_achievement",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    achievement_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_achievement_pkey", x => x.id);
                    table.ForeignKey(
                        name: "user_achievement_achievement_id_fkey",
                        column: x => x.achievement_id,
                        principalTable: "achievements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "article_category_translations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    article_category_id = table.Column<int>(type: "integer", nullable: false),
                    language_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name_value = table.Column<string>(type: "text", nullable: false),
                    description_value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("article_category_translations_pkey", x => x.id);
                    table.ForeignKey(
                        name: "article_category_translations_article_category_id_fkey",
                        column: x => x.article_category_id,
                        principalTable: "article_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "article_attachments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    article_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("article_attachments_pkey", x => x.id);
                    table.ForeignKey(
                        name: "article_attachments_article_fkey",
                        column: x => x.article_id,
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "article_category_assignment",
                columns: table => new
                {
                    article_id = table.Column<int>(type: "integer", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("article_category_assignment_pkey", x => new { x.article_id, x.category_id });
                    table.ForeignKey(
                        name: "article_category_assignment_article_id_fkey",
                        column: x => x.article_id,
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "article_category_assignment_category_id_fkey",
                        column: x => x.category_id,
                        principalTable: "article_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "article_translations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    article_id = table.Column<int>(type: "integer", nullable: false),
                    language_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name_value = table.Column<string>(type: "text", nullable: false),
                    description_value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("article_translations_pkey", x => x.id);
                    table.ForeignKey(
                        name: "article_translations_article_id_fkey",
                        column: x => x.article_id,
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "filtered_recording_parts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    start_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    end_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    probability_vector = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    state = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)0),
                    representant_flag = table.Column<bool>(type: "boolean", nullable: true),
                    recording_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("filtered_recording_parts_pkey", x => x.id);
                    table.ForeignKey(
                        name: "filtered_recording_parts_recording_id_fkey",
                        column: x => x.recording_id,
                        principalTable: "recordings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recording_parts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    recording_id = table.Column<int>(type: "integer", nullable: true),
                    start_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    end_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    gps_latitude_start = table.Column<decimal>(type: "numeric", nullable: true),
                    gps_longitude_start = table.Column<decimal>(type: "numeric", nullable: true),
                    gps_latitude_end = table.Column<decimal>(type: "numeric", nullable: true),
                    gps_longitude_end = table.Column<decimal>(type: "numeric", nullable: true),
                    file_path = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    length = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("recording_parts_pkey", x => x.id);
                    table.ForeignKey(
                        name: "recording_parts_recording_id_fkey",
                        column: x => x.recording_id,
                        principalTable: "recordings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "detected_dialects",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_guess_dialect_id = table.Column<int>(type: "integer", nullable: true),
                    confirmed_dialect_id = table.Column<int>(type: "integer", nullable: true),
                    filtered_recording_part_id = table.Column<int>(type: "integer", nullable: false),
                    predicted_dialect_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("detected_dialects_pkey", x => x.id);
                    table.ForeignKey(
                        name: "detected_dialects_confirmed_dialect_id_fkey",
                        column: x => x.confirmed_dialect_id,
                        principalTable: "dialects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "detected_dialects_filtered_recording_part_id_fkey",
                        column: x => x.filtered_recording_part_id,
                        principalTable: "filtered_recording_parts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "detected_dialects_user_guess_dialect_id_fkey",
                        column: x => x.user_guess_dialect_id,
                        principalTable: "dialects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_achievement_content_achievement_id",
                table: "achievement_content",
                column: "achievement_id");

            migrationBuilder.CreateIndex(
                name: "IX_article_attachments_article_id",
                table: "article_attachments",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "IX_article_category_assignment_category_id",
                table: "article_category_assignment",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_article_category_translations_article_category_id",
                table: "article_category_translations",
                column: "article_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_article_translations_article_id",
                table: "article_translations",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "detected_dialects_filtered_part_id",
                table: "detected_dialects",
                column: "filtered_recording_part_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_detected_dialects_confirmed_dialect_id",
                table: "detected_dialects",
                column: "confirmed_dialect_id");

            migrationBuilder.CreateIndex(
                name: "IX_detected_dialects_user_guess_dialect_id",
                table: "detected_dialects",
                column: "user_guess_dialect_id");

            migrationBuilder.CreateIndex(
                name: "devices_fcm_token_key",
                table: "devices",
                column: "fcm_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_filtered_recording_parts_recording_id",
                table: "filtered_recording_parts",
                column: "recording_id");

            migrationBuilder.CreateIndex(
                name: "IX_recording_parts_recording_id",
                table: "recording_parts",
                column: "recording_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_achievement_achievement_id",
                table: "user_achievement",
                column: "achievement_id");

            migrationBuilder.CreateIndex(
                name: "user_achievement_unique",
                table: "user_achievement",
                columns: new[] { "user_id", "achievement_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "achievement_content");

            migrationBuilder.DropTable(
                name: "article_attachments");

            migrationBuilder.DropTable(
                name: "article_category_assignment");

            migrationBuilder.DropTable(
                name: "article_category_translations");

            migrationBuilder.DropTable(
                name: "article_translations");

            migrationBuilder.DropTable(
                name: "detected_dialects");

            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "recording_parts");

            migrationBuilder.DropTable(
                name: "user_achievement");

            migrationBuilder.DropTable(
                name: "article_categories");

            migrationBuilder.DropTable(
                name: "articles");

            migrationBuilder.DropTable(
                name: "dialects");

            migrationBuilder.DropTable(
                name: "filtered_recording_parts");

            migrationBuilder.DropTable(
                name: "achievements");

            migrationBuilder.DropTable(
                name: "recordings");
        }
    }
}
