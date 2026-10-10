using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTablaAnalisisDetallado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analisis_detallado",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    contexto_json = table.Column<string>(type: "jsonb", nullable: false),
                    criterios_json = table.Column<string>(type: "jsonb", nullable: false),
                    puntos_json = table.Column<string>(type: "jsonb", nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    fecha_actualizacion = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analisis_detallado", x => x.id);
                    table.ForeignKey(
                        name: "FK_analisis_detallado_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "app",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analisis_detallado_usuario_id",
                schema: "app",
                table: "analisis_detallado",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analisis_detallado",
                schema: "app");
        }
    }
}
