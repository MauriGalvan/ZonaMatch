using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueParticipantPerGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Participant_group_id",
                schema: "app",
                table: "Participant");

            migrationBuilder.CreateIndex(
                name: "IX_Participant_group_id_user_id",
                schema: "app",
                table: "Participant",
                columns: new[] { "group_id", "user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Participant_group_id_user_id",
                schema: "app",
                table: "Participant");

            migrationBuilder.CreateIndex(
                name: "IX_Participant_group_id",
                schema: "app",
                table: "Participant",
                column: "group_id");
        }
    }
}
