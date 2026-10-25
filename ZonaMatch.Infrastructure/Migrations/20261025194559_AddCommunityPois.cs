using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityPois : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "es_administrador",
                schema: "app",
                table: "usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "resuelto_por_id",
                schema: "app",
                table: "aportes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "community_pois",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_new = table.Column<bool>(type: "boolean", nullable: false),
                    zone_slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    location = table.Column<Point>(type: "geometry(Point,4326)", nullable: true),
                    removed = table.Column<bool>(type: "boolean", nullable: false),
                    last_contribution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_pois", x => x.id);
                    table.ForeignKey(
                        name: "FK_community_pois_aportes_last_contribution_id",
                        column: x => x.last_contribution_id,
                        principalSchema: "app",
                        principalTable: "aportes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_aportes_resuelto_por_id",
                schema: "app",
                table: "aportes",
                column: "resuelto_por_id");

            migrationBuilder.CreateIndex(
                name: "IX_community_pois_last_contribution_id",
                schema: "app",
                table: "community_pois",
                column: "last_contribution_id");

            migrationBuilder.CreateIndex(
                name: "IX_community_pois_location",
                schema: "app",
                table: "community_pois",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.AddForeignKey(
                name: "FK_aportes_usuarios_resuelto_por_id",
                schema: "app",
                table: "aportes",
                column: "resuelto_por_id",
                principalSchema: "app",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Contributions approved before this table existed: the new points, and for each corrected place the
            // last approved value of each reason ("Otro" is informative only)
            migrationBuilder.Sql("""
                INSERT INTO app.community_pois
                    (id, is_new, zone_slug, name, category, location, removed, last_contribution_id, updated_at)
                SELECT 'a' || a.id::text, true, a.zona_slug, a.nombre, a.categoria, a.ubicacion, false, a.id,
                       COALESCE(a.fecha_resolucion, a.fecha_creacion)
                FROM app.aportes a
                WHERE a.tipo = 'PuntoNuevo' AND a.estado = 'Aprobado';

                WITH latest AS (
                    SELECT DISTINCT ON (a.punto_interes_id, a.motivo)
                           a.punto_interes_id, a.motivo, a.nombre, a.categoria, a.ubicacion, a.id, a.zona_slug,
                           COALESCE(a.fecha_resolucion, a.fecha_creacion) AS resolved_at
                    FROM app.aportes a
                    WHERE a.tipo = 'Correccion' AND a.estado = 'Aprobado' AND a.motivo <> 'Otro'
                          AND a.punto_interes_id IS NOT NULL
                    ORDER BY a.punto_interes_id, a.motivo, COALESCE(a.fecha_resolucion, a.fecha_creacion) DESC
                )
                INSERT INTO app.community_pois AS p
                    (id, is_new, zone_slug, name, category, location, removed, last_contribution_id, updated_at)
                SELECT punto_interes_id, false, min(zona_slug),
                       max(nombre) FILTER (WHERE motivo = 'Nombre'),
                       max(categoria) FILTER (WHERE motivo = 'Categoria'),
                       (array_agg(ubicacion) FILTER (WHERE motivo = 'Ubicacion'))[1],
                       bool_or(motivo = 'Cerro'),
                       (array_agg(id ORDER BY resolved_at DESC))[1],
                       max(resolved_at)
                FROM latest
                GROUP BY punto_interes_id
                ON CONFLICT (id) DO UPDATE SET
                    name = COALESCE(EXCLUDED.name, p.name),
                    category = COALESCE(EXCLUDED.category, p.category),
                    location = COALESCE(EXCLUDED.location, p.location),
                    removed = p.removed OR EXCLUDED.removed,
                    last_contribution_id = EXCLUDED.last_contribution_id,
                    updated_at = EXCLUDED.updated_at;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_aportes_usuarios_resuelto_por_id",
                schema: "app",
                table: "aportes");

            migrationBuilder.DropTable(
                name: "community_pois",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "IX_aportes_resuelto_por_id",
                schema: "app",
                table: "aportes");

            migrationBuilder.DropColumn(
                name: "es_administrador",
                schema: "app",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "resuelto_por_id",
                schema: "app",
                table: "aportes");
        }
    }
}
