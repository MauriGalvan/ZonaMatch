using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAmbientesAlContexto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ambientes",
                schema: "app",
                table: "contextos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_contextos_ambientes",
                schema: "app",
                table: "contextos",
                sql: "ambientes IS NULL OR ambientes BETWEEN 1 AND 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_contextos_ambientes",
                schema: "app",
                table: "contextos");

            migrationBuilder.DropColumn(
                name: "ambientes",
                schema: "app",
                table: "contextos");
        }
    }
}
