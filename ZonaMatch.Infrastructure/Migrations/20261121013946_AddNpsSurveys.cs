using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNpsSurveys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "active_survey",
                schema: "app",
                table: "user_feedback_preferences",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "completed_zone_analyses",
                schema: "app",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_completed_zone_analyses", x => new { x.user_id, x.fingerprint });
                    table.ForeignKey(
                        name: "FK_completed_zone_analyses_usuarios_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nps_surveys",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    postponed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    score = table.Column<int>(type: "integer", nullable: true),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nps_surveys", x => x.id);
                    table.CheckConstraint("ck_nps_completion", "(completed_at IS NULL AND score IS NULL) OR (completed_at IS NOT NULL AND score IS NOT NULL AND postponed_at IS NULL)");
                    table.CheckConstraint("ck_nps_score", "score IS NULL OR score BETWEEN 0 AND 10");
                    table.ForeignKey(
                        name: "FK_nps_surveys_usuarios_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_nps_surveys_completed_at",
                schema: "app",
                table: "nps_surveys",
                column: "completed_at");

            migrationBuilder.CreateIndex(
                name: "IX_nps_surveys_user_id",
                schema: "app",
                table: "nps_surveys",
                column: "user_id",
                unique: true,
                filter: "completed_at IS NULL AND postponed_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "completed_zone_analyses",
                schema: "app");

            migrationBuilder.DropTable(
                name: "nps_surveys",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "active_survey",
                schema: "app",
                table: "user_feedback_preferences");
        }
    }
}
