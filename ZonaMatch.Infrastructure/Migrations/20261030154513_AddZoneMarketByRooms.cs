using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneMarketByRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "zone_market_rooms",
                schema: "app",
                columns: table => new
                {
                    zone_slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    operation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    property_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rooms = table.Column<int>(type: "integer", nullable: false),
                    listings = table.Column<int>(type: "integer", nullable: true),
                    sample_size = table.Column<int>(type: "integer", nullable: false),
                    average_price = table.Column<decimal>(type: "numeric(14,0)", nullable: true),
                    median_price = table.Column<decimal>(type: "numeric(14,0)", nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    average_price_m2 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    median_price_m2 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    average_expenses_ars = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    median_covered_m2 = table.Column<decimal>(type: "numeric(8,0)", nullable: true),
                    median_total_m2 = table.Column<decimal>(type: "numeric(8,0)", nullable: true),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    inherited_from = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zone_market_rooms", x => new { x.zone_slug, x.operation, x.property_type, x.rooms });
                    table.CheckConstraint("ck_zone_market_rooms_counts", "listings >= 0 AND sample_size >= 0");
                    table.CheckConstraint("ck_zone_market_rooms_currency", "currency IN ('ARS', 'USD')");
                    table.CheckConstraint("ck_zone_market_rooms_operation", "operation IN ('sale', 'rent')");
                    table.CheckConstraint("ck_zone_market_rooms_property_type", "property_type IN ('apartment', 'house', 'ph', 'all')");
                    table.CheckConstraint("ck_zone_market_rooms_rooms", "rooms BETWEEN 1 AND 5");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "zone_market_rooms",
                schema: "app");
        }
    }
}
