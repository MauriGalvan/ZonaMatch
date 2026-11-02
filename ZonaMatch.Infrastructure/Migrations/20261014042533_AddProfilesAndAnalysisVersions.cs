using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProfilesAndAnalysisVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Each context represented a saved analysis; keep them as copies of type "analysis".
            migrationBuilder.AddColumn<string>(
                name: "tipo",
                schema: "app",
                table: "contextos",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "analisis");

            migrationBuilder.CreateTable(
                name: "analisis",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    contexto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    analisis_origen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    fecha_actualizacion = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analisis", x => x.id);
                    table.ForeignKey(
                        name: "FK_analisis_analisis_analisis_origen_id",
                        column: x => x.analisis_origen_id,
                        principalSchema: "app",
                        principalTable: "analisis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_analisis_contextos_contexto_id",
                        column: x => x.contexto_id,
                        principalSchema: "app",
                        principalTable: "contextos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_analisis_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "app",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "analisis_zonas",
                schema: "app",
                columns: table => new
                {
                    analisis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    zona_slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    es_inicial = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analisis_zonas", x => new { x.analisis_id, x.orden });
                    table.ForeignKey(
                        name: "FK_analisis_zonas_analisis_analisis_id",
                        column: x => x.analisis_id,
                        principalSchema: "app",
                        principalTable: "analisis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Move each saved analysis to the new table with the same ID so existing frontend URLs keep
            // working. Keep its context as a criteria snapshot. Older records did not save zones.
            migrationBuilder.Sql(
                "INSERT INTO app.analisis (id, usuario_id, nombre, contexto_id, analisis_origen_id, fecha_creacion, fecha_actualizacion) " +
                "SELECT id, usuario_id, nombre, id, NULL, fecha_creacion, fecha_actualizacion FROM app.contextos;");

            migrationBuilder.DropColumn(
                name: "nombre",
                schema: "app",
                table: "contextos");

            migrationBuilder.DropIndex(
                name: "IX_contextos_usuario_id",
                schema: "app",
                table: "contextos");

            migrationBuilder.CreateIndex(
                name: "ux_contextos_perfil_usuario",
                schema: "app",
                table: "contextos",
                column: "usuario_id",
                unique: true,
                filter: "tipo = 'perfil'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_contextos_tipo",
                schema: "app",
                table: "contextos",
                sql: "tipo IN ('perfil', 'analisis')");

            migrationBuilder.CreateIndex(
                name: "IX_analisis_analisis_origen_id",
                schema: "app",
                table: "analisis",
                column: "analisis_origen_id");

            migrationBuilder.CreateIndex(
                name: "IX_analisis_contexto_id",
                schema: "app",
                table: "analisis",
                column: "contexto_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_analisis_usuario_id",
                schema: "app",
                table: "analisis",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "nombre",
                schema: "app",
                table: "contextos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // Restore the previous model (one context per analysis); profiles did not exist and are removed.
            migrationBuilder.Sql(
                "UPDATE app.contextos c SET nombre = a.nombre FROM app.analisis a WHERE a.contexto_id = c.id; " +
                "DELETE FROM app.contextos WHERE tipo = 'perfil';");

            migrationBuilder.DropTable(
                name: "analisis_zonas",
                schema: "app");

            migrationBuilder.DropTable(
                name: "analisis",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "ux_contextos_perfil_usuario",
                schema: "app",
                table: "contextos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_contextos_tipo",
                schema: "app",
                table: "contextos");

            migrationBuilder.DropColumn(
                name: "tipo",
                schema: "app",
                table: "contextos");

            migrationBuilder.CreateIndex(
                name: "IX_contextos_usuario_id",
                schema: "app",
                table: "contextos",
                column: "usuario_id");
        }
    }
}
