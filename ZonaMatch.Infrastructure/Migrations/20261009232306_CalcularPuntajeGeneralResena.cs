using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CalcularPuntajeGeneralResena : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_resenas_puntajes",
                schema: "app",
                table: "resenas");

            migrationBuilder.DropColumn(
                name: "puntaje",
                schema: "app",
                table: "resenas");

            migrationBuilder.AddCheckConstraint(
                name: "ck_resenas_puntajes",
                schema: "app",
                table: "resenas",
                sql: "puntaje_seguridad BETWEEN 1 AND 5 AND puntaje_transporte BETWEEN 1 AND 5 AND puntaje_conectividad BETWEEN 1 AND 5 AND puntaje_comercios BETWEEN 1 AND 5 AND puntaje_espacios_verdes BETWEEN 1 AND 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_resenas_puntajes",
                schema: "app",
                table: "resenas");

            migrationBuilder.AddColumn<int>(
                name: "puntaje",
                schema: "app",
                table: "resenas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Vuelve a guardar el puntaje general redondeado, para que las resenas existentes cumplan el check
            migrationBuilder.Sql(
                "UPDATE app.resenas SET puntaje = ROUND((puntaje_seguridad + puntaje_transporte + puntaje_conectividad " +
                "+ puntaje_comercios + puntaje_espacios_verdes) / 5.0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_resenas_puntajes",
                schema: "app",
                table: "resenas",
                sql: "puntaje BETWEEN 1 AND 5 AND puntaje_seguridad BETWEEN 1 AND 5 AND puntaje_transporte BETWEEN 1 AND 5 AND puntaje_conectividad BETWEEN 1 AND 5 AND puntaje_comercios BETWEEN 1 AND 5 AND puntaje_espacios_verdes BETWEEN 1 AND 5");
        }
    }
}
