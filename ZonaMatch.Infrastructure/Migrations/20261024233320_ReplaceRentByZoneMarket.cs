using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceRentByZoneMarket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "zone_market",
                schema: "app",
                columns: table => new
                {
                    zone_slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    operation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    property_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    listings = table.Column<int>(type: "integer", nullable: true),
                    sample_size = table.Column<int>(type: "integer", nullable: false),
                    average_price = table.Column<decimal>(type: "numeric(14,0)", nullable: true),
                    median_price = table.Column<decimal>(type: "numeric(14,0)", nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    average_price_m2 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    median_price_m2 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    average_expenses_ars = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    inherited_from = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zone_market", x => new { x.zone_slug, x.operation, x.property_type });
                    table.CheckConstraint("ck_zone_market_counts", "listings >= 0 AND sample_size >= 0");
                    table.CheckConstraint("ck_zone_market_currency", "currency IN ('ARS', 'USD')");
                    table.CheckConstraint("ck_zone_market_operation", "operation IN ('sale', 'rent')");
                    table.CheckConstraint("ck_zone_market_prices", "average_price >= 0 AND median_price >= 0");
                    table.CheckConstraint("ck_zone_market_property_type", "property_type IN ('apartment', 'house', 'ph', 'all')");
                });

            // Keep the rents already loaded: they become the rent row for every property type
            migrationBuilder.Sql("""
                INSERT INTO app.zone_market (zone_slug, operation, property_type, listings, sample_size, average_price,
                                             currency, source, updated_at)
                SELECT zona_slug, 'rent', 'all', propiedades_disponibles, 0, alquiler_promedio, 'ARS',
                       CASE fuente WHEN 'estimado' THEN 'estimated' ELSE fuente END, fecha_actualizacion
                FROM app.alquileres_zona;
                """);

            migrationBuilder.DropTable(
                name: "alquileres_zona",
                schema: "app");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alquileres_zona",
                schema: "app",
                columns: table => new
                {
                    zona_slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    alquiler_promedio = table.Column<decimal>(type: "numeric(12,0)", nullable: false),
                    fecha_actualizacion = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    fuente = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    propiedades_disponibles = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alquileres_zona", x => x.zona_slug);
                    table.CheckConstraint("ck_alquileres_zona_promedio", "alquiler_promedio >= 0");
                    table.CheckConstraint("ck_alquileres_zona_propiedades", "propiedades_disponibles >= 0");
                });

            migrationBuilder.Sql("""
                INSERT INTO app.alquileres_zona (zona_slug, alquiler_promedio, propiedades_disponibles, fuente,
                                                 fecha_actualizacion)
                SELECT zone_slug, average_price, coalesce(listings, 0),
                       CASE source WHEN 'estimated' THEN 'estimado' ELSE source END, updated_at
                FROM app.zone_market
                WHERE operation = 'rent' AND property_type = 'all' AND average_price IS NOT NULL;
                """);

            migrationBuilder.DropTable(
                name: "zone_market",
                schema: "app");
        }
    }
}
