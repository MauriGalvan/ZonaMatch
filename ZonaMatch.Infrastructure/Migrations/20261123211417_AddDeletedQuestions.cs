using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletedQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "deleted_questions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    author_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    asked_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    answer_count = table.Column<int>(type: "integer", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    deleted_by_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deleted_questions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_deleted_questions_deleted_at",
                schema: "app",
                table: "deleted_questions",
                column: "deleted_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deleted_questions",
                schema: "app");
        }
    }
}
