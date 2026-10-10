using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPuntosInteresUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tags_punto_interes",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nombre_normalizado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags_punto_interes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "puntos_interes_usuario",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alias = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ubicacion = table.Column<Point>(type: "geometry(Point,4326)", nullable: false),
                    direccion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    barrio = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_puntos_interes_usuario", x => x.id);
                    table.ForeignKey(
                        name: "FK_puntos_interes_usuario_tags_punto_interes_tag_id",
                        column: x => x.tag_id,
                        principalSchema: "app",
                        principalTable: "tags_punto_interes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_puntos_interes_usuario_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "app",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_puntos_interes_usuario_tag_id",
                schema: "app",
                table: "puntos_interes_usuario",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "IX_puntos_interes_usuario_ubicacion",
                schema: "app",
                table: "puntos_interes_usuario",
                column: "ubicacion")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_puntos_interes_usuario_usuario_id",
                schema: "app",
                table: "puntos_interes_usuario",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_tags_punto_interes_nombre_normalizado",
                schema: "app",
                table: "tags_punto_interes",
                column: "nombre_normalizado",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "puntos_interes_usuario",
                schema: "app");

            migrationBuilder.DropTable(
                name: "tags_punto_interes",
                schema: "app");
        }
    }
}
