using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SimplificarMovilidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "movilidad",
                schema: "app",
                table: "contextos",
                type: "text[]",
                nullable: true);

            // La movilidad pasa de la tabla intermedia a una lista simple en contextos,
            // igual que mascotas. Se copia antes de dropear contexto_transportes, y el
            // orden es el del catalogo viejo (que desaparece en esta misma migracion).
            migrationBuilder.Sql("""
                UPDATE app.contextos c
                SET movilidad = (
                    SELECT array_agg(ct.transporte_id ORDER BY t.orden)
                    FROM app.contexto_transportes ct
                    JOIN app.transportes t ON t.id = ct.transporte_id
                    WHERE ct.contexto_id = c.id);

                UPDATE app.contextos
                SET movilidad = ARRAY[]::text[]
                WHERE movilidad IS NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_puntos_interes_transportes_transporte_id",
                schema: "app",
                table: "puntos_interes");

            migrationBuilder.DropTable(
                name: "contexto_transportes",
                schema: "app");

            migrationBuilder.DropTable(
                name: "transportes",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "IX_puntos_interes_transporte_id",
                schema: "app",
                table: "puntos_interes");

            migrationBuilder.DropColumn(
                name: "transporte_id",
                schema: "app",
                table: "puntos_interes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "movilidad",
                schema: "app",
                table: "contextos");

            migrationBuilder.AddColumn<string>(
                name: "transporte_id",
                schema: "app",
                table: "puntos_interes",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "transportes",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transportes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contexto_transportes",
                schema: "app",
                columns: table => new
                {
                    contexto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transporte_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contexto_transportes", x => new { x.contexto_id, x.transporte_id });
                    table.ForeignKey(
                        name: "FK_contexto_transportes_contextos_contexto_id",
                        column: x => x.contexto_id,
                        principalSchema: "app",
                        principalTable: "contextos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contexto_transportes_transportes_transporte_id",
                        column: x => x.transporte_id,
                        principalSchema: "app",
                        principalTable: "transportes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_puntos_interes_transporte_id",
                schema: "app",
                table: "puntos_interes",
                column: "transporte_id");

            migrationBuilder.CreateIndex(
                name: "IX_contexto_transportes_transporte_id",
                schema: "app",
                table: "contexto_transportes",
                column: "transporte_id");

            migrationBuilder.CreateIndex(
                name: "IX_transportes_orden",
                schema: "app",
                table: "transportes",
                column: "orden");

            migrationBuilder.AddForeignKey(
                name: "FK_puntos_interes_transportes_transporte_id",
                schema: "app",
                table: "puntos_interes",
                column: "transporte_id",
                principalSchema: "app",
                principalTable: "transportes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
