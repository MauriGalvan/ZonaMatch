using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSuperficieAlContexto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "superficie_m2",
                schema: "app",
                table: "contextos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_contextos_superficie_m2",
                schema: "app",
                table: "contextos",
                sql: "superficie_m2 IS NULL OR superficie_m2 BETWEEN 10 AND 2000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_contextos_superficie_m2",
                schema: "app",
                table: "contextos");

            migrationBuilder.DropColumn(
                name: "superficie_m2",
                schema: "app",
                table: "contextos");
        }
    }
}
