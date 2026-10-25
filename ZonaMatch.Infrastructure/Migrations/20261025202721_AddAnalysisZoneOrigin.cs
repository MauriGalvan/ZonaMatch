using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisZoneOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "origen_latitud",
                schema: "app",
                table: "analisis_zonas",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "origen_longitud",
                schema: "app",
                table: "analisis_zonas",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "origen_latitud",
                schema: "app",
                table: "analisis_zonas");

            migrationBuilder.DropColumn(
                name: "origen_longitud",
                schema: "app",
                table: "analisis_zonas");
        }
    }
}
