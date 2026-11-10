using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisFollowUps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analysis_follow_ups",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invited_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    postponed_until = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    helpfulness = table.Column<int>(type: "integer", nullable: true),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_follow_ups", x => x.id);
                    table.CheckConstraint("ck_analysis_follow_ups_completion", "(completed_at IS NULL AND outcome IS NULL AND helpfulness IS NULL) OR (completed_at IS NOT NULL AND outcome IS NOT NULL AND helpfulness IS NOT NULL)");
                    table.CheckConstraint("ck_analysis_follow_ups_helpfulness", "helpfulness IS NULL OR helpfulness BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_analysis_follow_ups_outcome", "outcome IS NULL OR outcome IN ('still-searching', 'visited-recommended-zone', 'chose-recommended-zone', 'chose-another-zone', 'stopped-searching')");
                    table.ForeignKey(
                        name: "FK_analysis_follow_ups_analisis_analysis_id",
                        column: x => x.analysis_id,
                        principalSchema: "app",
                        principalTable: "analisis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_analysis_follow_ups_usuarios_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_feedback_preferences",
                schema: "app",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    surveys_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    next_invitation_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_feedback_preferences", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_user_feedback_preferences_usuarios_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analysis_follow_ups_analysis_id",
                schema: "app",
                table: "analysis_follow_ups",
                column: "analysis_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_analysis_follow_ups_user_id_completed_at_postponed_until",
                schema: "app",
                table: "analysis_follow_ups",
                columns: new[] { "user_id", "completed_at", "postponed_until" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analysis_follow_ups",
                schema: "app");

            migrationBuilder.DropTable(
                name: "user_feedback_preferences",
                schema: "app");
        }
    }
}
