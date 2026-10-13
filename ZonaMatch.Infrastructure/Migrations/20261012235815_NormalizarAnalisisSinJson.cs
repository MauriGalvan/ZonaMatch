using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizarAnalisisSinJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contextos",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    personas = table.Column<int>(type: "integer", nullable: false),
                    mascotas = table.Column<string[]>(type: "text[]", nullable: true),
                    alquiler_maximo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    fecha_actualizacion = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contextos", x => x.id);
                    table.ForeignKey(
                        name: "FK_contextos_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "app",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "contexto_criterios",
                schema: "app",
                columns: table => new
                {
                    contexto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criterio_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    prioridad = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contexto_criterios", x => new { x.contexto_id, x.criterio_id });
                    table.CheckConstraint("ck_contexto_criterios_prioridad", "prioridad IN ('baja', 'media', 'alta')");
                    table.ForeignKey(
                        name: "FK_contexto_criterios_contextos_contexto_id",
                        column: x => x.contexto_id,
                        principalSchema: "app",
                        principalTable: "contextos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contexto_criterios_criterios_criterio_id",
                        column: x => x.criterio_id,
                        principalSchema: "app",
                        principalTable: "criterios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "personas",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contexto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    edad = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personas", x => x.id);
                    table.ForeignKey(
                        name: "FK_personas_contextos_contexto_id",
                        column: x => x.contexto_id,
                        principalSchema: "app",
                        principalTable: "contextos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateTable(
                name: "puntos_interes",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contexto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    direccion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    transporte_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    quien_va = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    frecuencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    prioridad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tiempo_max = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_puntos_interes", x => x.id);
                    table.ForeignKey(
                        name: "FK_puntos_interes_contextos_contexto_id",
                        column: x => x.contexto_id,
                        principalSchema: "app",
                        principalTable: "contextos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_puntos_interes_transportes_transporte_id",
                        column: x => x.transporte_id,
                        principalSchema: "app",
                        principalTable: "transportes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contexto_criterios_criterio_id",
                schema: "app",
                table: "contexto_criterios",
                column: "criterio_id");

            migrationBuilder.CreateIndex(
                name: "IX_contexto_transportes_transporte_id",
                schema: "app",
                table: "contexto_transportes",
                column: "transporte_id");

            migrationBuilder.CreateIndex(
                name: "IX_contextos_usuario_id",
                schema: "app",
                table: "contextos",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_personas_contexto_id_orden",
                schema: "app",
                table: "personas",
                columns: new[] { "contexto_id", "orden" });

            migrationBuilder.CreateIndex(
                name: "IX_puntos_interes_contexto_id_orden",
                schema: "app",
                table: "puntos_interes",
                columns: new[] { "contexto_id", "orden" });

            migrationBuilder.CreateIndex(
                name: "IX_puntos_interes_transporte_id",
                schema: "app",
                table: "puntos_interes",
                column: "transporte_id");

            migrationBuilder.CreateIndex(
                name: "IX_transportes_orden",
                schema: "app",
                table: "transportes",
                column: "orden");

            // Seed del catalogo de transportes del wizard. SQL a mano y no HasData, igual
            // que el seed de criterios: la data se puede editar en la BD sin que una
            // migracion futura la pise.
            migrationBuilder.Sql("""
                INSERT INTO app.transportes (id, nombre, orden)
                VALUES
                    ('a-pie', 'A pie', 1),
                    ('transporte-publico', 'Transporte público', 2),
                    ('vehiculo', 'Vehículo propio', 3),
                    ('bici', 'Bici / monopatín', 4)
                ON CONFLICT (id) DO NOTHING;
                """);

            // Copia de los datos viejos: analisis_detallado (jsonb) -> tablas normalizadas.
            // Todo es tolerante a formats raros: lo que no se pueda interpretar se omite o
            // se guarda null, y la tabla vieja recien ahi se borra.
            migrationBuilder.Sql("""
                INSERT INTO app.contextos (id, usuario_id, nombre, personas, mascotas, alquiler_maximo, fecha_creacion, fecha_actualizacion)
                SELECT a.id,
                       a.usuario_id,
                       a.nombre,
                       GREATEST(
                           CASE WHEN a.contexto_json->>'personas' ~ '^[0-9]+$'
                                THEN (a.contexto_json->>'personas')::int
                                ELSE 1 END,
                           1),
                       CASE WHEN jsonb_typeof(a.contexto_json->'mascotas') = 'array'
                            THEN ARRAY(SELECT jsonb_array_elements_text(a.contexto_json->'mascotas'))
                            ELSE ARRAY[]::text[] END,
                       LEFT(COALESCE(a.puntos_json->>'alquilerMaximo', ''), 30),
                       a.fecha_creacion,
                       a.fecha_actualizacion
                FROM app.analisis_detallado a;
                """);

            migrationBuilder.Sql("""
                INSERT INTO app.personas (id, contexto_id, orden, nombre, edad)
                SELECT gen_random_uuid(),
                       a.id,
                       g.orden,
                       'Persona ' || g.orden::text,
                       CASE WHEN jsonb_typeof(a.contexto_json->'edades') = 'array'
                            THEN a.contexto_json->'edades' -> (g.orden - 1) #>> '{}'
                       END
                FROM app.analisis_detallado a
                CROSS JOIN LATERAL generate_series(
                    1,
                    GREATEST(
                        CASE WHEN a.contexto_json->>'personas' ~ '^[0-9]+$'
                             THEN (a.contexto_json->>'personas')::int
                             ELSE 1 END,
                        CASE WHEN jsonb_typeof(a.contexto_json->'edades') = 'array'
                             THEN jsonb_array_length(a.contexto_json->'edades')
                             ELSE 0 END)) AS g(orden);
                """);

            migrationBuilder.Sql("""
                INSERT INTO app.puntos_interes (id, contexto_id, orden, nombre, direccion, latitude, longitude, tipo, quien_va, frecuencia, prioridad, tiempo_max)
                SELECT gen_random_uuid(),
                       a.id,
                       t.orden,
                       COALESCE(LEFT(lugar->>'nombre', 100), ''),
                       LEFT(lugar->>'direccion', 200),
                       CASE WHEN lugar->>'latitude' ~ '^-?[0-9]+(\.[0-9]+)?$'
                            THEN (lugar->>'latitude')::double precision END,
                       CASE WHEN lugar->>'longitude' ~ '^-?[0-9]+(\.[0-9]+)?$'
                            THEN (lugar->>'longitude')::double precision END,
                       LEFT(lugar->>'tipo', 30),
                       LEFT(lugar->>'quienVa', 20),
                       LEFT(lugar->>'frecuencia', 20),
                       LEFT(lugar->>'prioridad', 20),
                       LEFT(lugar->>'tiempoMax', 10)
                FROM app.analisis_detallado a
                CROSS JOIN LATERAL (
                    SELECT elem AS lugar, ord AS orden
                    FROM jsonb_array_elements(
                        CASE WHEN jsonb_typeof(a.puntos_json->'lugares') = 'array'
                             THEN a.puntos_json->'lugares'
                             ELSE '[]'::jsonb END) WITH ORDINALITY AS x(elem, ord)) AS t;
                """);

            migrationBuilder.Sql("""
                INSERT INTO app.contexto_criterios (contexto_id, criterio_id, prioridad)
                SELECT a.id, e.clave, e.valor #>> '{}'
                FROM app.analisis_detallado a
                CROSS JOIN LATERAL jsonb_each(
                    CASE WHEN jsonb_typeof(a.criterios_json) = 'object'
                         THEN a.criterios_json
                         ELSE '{}'::jsonb END) AS e(clave, valor)
                JOIN app.criterios c ON c.id = e.clave
                WHERE e.valor #>> '{}' IN ('baja', 'media', 'alta');
                """);

            migrationBuilder.Sql("""
                INSERT INTO app.contexto_transportes (contexto_id, transporte_id)
                SELECT DISTINCT a.id, m.transporte_id
                FROM app.analisis_detallado a
                CROSS JOIN LATERAL (
                    SELECT CASE WHEN jsonb_typeof(a.contexto_json->'movilidad') = 'array'
                                THEN elem #>> '{}'
                           END AS transporte_id
                    FROM jsonb_array_elements(
                        CASE WHEN jsonb_typeof(a.contexto_json->'movilidad') = 'array'
                             THEN a.contexto_json->'movilidad'
                             ELSE '[]'::jsonb END) AS x(elem)) AS m
                JOIN app.transportes tr ON tr.id = m.transporte_id;
                """);

            migrationBuilder.DropTable(
                name: "analisis_detallado",
                schema: "app");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contexto_criterios",
                schema: "app");

            migrationBuilder.DropTable(
                name: "contexto_transportes",
                schema: "app");

            migrationBuilder.DropTable(
                name: "personas",
                schema: "app");

            migrationBuilder.DropTable(
                name: "puntos_interes",
                schema: "app");

            migrationBuilder.DropTable(
                name: "contextos",
                schema: "app");

            migrationBuilder.DropTable(
                name: "transportes",
                schema: "app");

            migrationBuilder.CreateTable(
                name: "analisis_detallado",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contexto_json = table.Column<string>(type: "jsonb", nullable: false),
                    criterios_json = table.Column<string>(type: "jsonb", nullable: false),
                    fecha_actualizacion = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    puntos_json = table.Column<string>(type: "jsonb", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false)
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
    }
}
