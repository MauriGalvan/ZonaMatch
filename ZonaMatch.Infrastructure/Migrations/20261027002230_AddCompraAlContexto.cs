using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompraAlContexto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "compra_maxima_usd",
                schema: "app",
                table: "contextos",
                type: "numeric(12,0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "operacion",
                schema: "app",
                table: "contextos",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "alquiler");

            migrationBuilder.AddColumn<string>(
                name: "tipo_vivienda",
                schema: "app",
                table: "contextos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_contextos_operacion",
                schema: "app",
                table: "contextos",
                sql: "operacion IN ('alquiler', 'compra')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_contextos_tipo_vivienda",
                schema: "app",
                table: "contextos",
                sql: "tipo_vivienda IS NULL OR tipo_vivienda IN ('apartment', 'house', 'ph')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_contextos_operacion",
                schema: "app",
                table: "contextos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_contextos_tipo_vivienda",
                schema: "app",
                table: "contextos");

            migrationBuilder.DropColumn(
                name: "compra_maxima_usd",
                schema: "app",
                table: "contextos");

            migrationBuilder.DropColumn(
                name: "operacion",
                schema: "app",
                table: "contextos");

            migrationBuilder.DropColumn(
                name: "tipo_vivienda",
                schema: "app",
                table: "contextos");
        }
    }
}
