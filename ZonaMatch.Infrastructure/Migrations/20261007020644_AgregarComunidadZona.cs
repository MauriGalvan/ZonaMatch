using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarComunidadZona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "aportes",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    zona_slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    categoria = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ubicacion = table.Column<Point>(type: "geometry(Point,4326)", nullable: true),
                    horario = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    direccion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    vive_o_trabaja_en_zona = table.Column<bool>(type: "boolean", nullable: false),
                    punto_interes_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    punto_interes_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    motivo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    comentario = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    fecha_resolucion = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aportes", x => x.id);
                    table.ForeignKey(
                        name: "FK_aportes_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "app",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "preguntas",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    zona_slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    texto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_preguntas", x => x.id);
                    table.ForeignKey(
                        name: "FK_preguntas_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "app",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resenas",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    zona_slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    puntaje = table.Column<int>(type: "integer", nullable: false),
                    puntaje_seguridad = table.Column<int>(type: "integer", nullable: false),
                    puntaje_transporte = table.Column<int>(type: "integer", nullable: false),
                    puntaje_conectividad = table.Column<int>(type: "integer", nullable: false),
                    puntaje_comercios = table.Column<int>(type: "integer", nullable: false),
                    puntaje_espacios_verdes = table.Column<int>(type: "integer", nullable: false),
                    texto = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    anios_en_zona = table.Column<int>(type: "integer", nullable: true),
                    temas = table.Column<List<string>>(type: "text[]", nullable: false),
                    verificada = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    fecha_actualizacion = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resenas", x => x.id);
                    table.CheckConstraint("ck_resenas_puntajes", "puntaje BETWEEN 1 AND 5 AND puntaje_seguridad BETWEEN 1 AND 5 AND puntaje_transporte BETWEEN 1 AND 5 AND puntaje_conectividad BETWEEN 1 AND 5 AND puntaje_comercios BETWEEN 1 AND 5 AND puntaje_espacios_verdes BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_resenas_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "app",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "aportes_validaciones",
                schema: "app",
                columns: table => new
                {
                    aporte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirma = table.Column<bool>(type: "boolean", nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aportes_validaciones", x => new { x.aporte_id, x.usuario_id });
                    table.ForeignKey(
                        name: "FK_aportes_validaciones_aportes_aporte_id",
                        column: x => x.aporte_id,
                        principalSchema: "app",
                        principalTable: "aportes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_aportes_validaciones_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "app",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "respuestas",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pregunta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    texto = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_respuestas", x => x.id);
                    table.ForeignKey(
                        name: "FK_respuestas_preguntas_pregunta_id",
                        column: x => x.pregunta_id,
                        principalSchema: "app",
                        principalTable: "preguntas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_respuestas_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "app",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resenas_votos_util",
                schema: "app",
                columns: table => new
                {
                    resena_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resenas_votos_util", x => new { x.resena_id, x.usuario_id });
                    table.ForeignKey(
                        name: "FK_resenas_votos_util_resenas_resena_id",
                        column: x => x.resena_id,
                        principalSchema: "app",
                        principalTable: "resenas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_resenas_votos_util_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "app",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_aportes_punto_interes_id_estado",
                schema: "app",
                table: "aportes",
                columns: new[] { "punto_interes_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_aportes_usuario_id",
                schema: "app",
                table: "aportes",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_aportes_zona_slug_estado",
                schema: "app",
                table: "aportes",
                columns: new[] { "zona_slug", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_aportes_validaciones_usuario_id",
                schema: "app",
                table: "aportes_validaciones",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_preguntas_usuario_id",
                schema: "app",
                table: "preguntas",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_preguntas_zona_slug",
                schema: "app",
                table: "preguntas",
                column: "zona_slug");

            migrationBuilder.CreateIndex(
                name: "IX_resenas_usuario_id",
                schema: "app",
                table: "resenas",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_resenas_zona_slug_usuario_id",
                schema: "app",
                table: "resenas",
                columns: new[] { "zona_slug", "usuario_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_resenas_votos_util_usuario_id",
                schema: "app",
                table: "resenas_votos_util",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_respuestas_pregunta_id",
                schema: "app",
                table: "respuestas",
                column: "pregunta_id");

            migrationBuilder.CreateIndex(
                name: "IX_respuestas_usuario_id",
                schema: "app",
                table: "respuestas",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aportes_validaciones",
                schema: "app");

            migrationBuilder.DropTable(
                name: "resenas_votos_util",
                schema: "app");

            migrationBuilder.DropTable(
                name: "respuestas",
                schema: "app");

            migrationBuilder.DropTable(
                name: "aportes",
                schema: "app");

            migrationBuilder.DropTable(
                name: "resenas",
                schema: "app");

            migrationBuilder.DropTable(
                name: "preguntas",
                schema: "app");
        }
    }
}
