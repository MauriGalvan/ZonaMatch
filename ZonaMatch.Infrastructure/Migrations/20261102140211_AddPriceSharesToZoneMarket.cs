using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceSharesToZoneMarket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal[]>(
                name: "price_m2_shares",
                schema: "app",
                table: "zone_market_rooms",
                type: "numeric[]",
                nullable: true);

            migrationBuilder.AddColumn<decimal[]>(
                name: "price_shares",
                schema: "app",
                table: "zone_market_rooms",
                type: "numeric[]",
                nullable: true);

            migrationBuilder.AddColumn<decimal[]>(
                name: "price_m2_shares",
                schema: "app",
                table: "zone_market",
                type: "numeric[]",
                nullable: true);

            migrationBuilder.AddColumn<decimal[]>(
                name: "price_shares",
                schema: "app",
                table: "zone_market",
                type: "numeric[]",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "price_m2_shares",
                schema: "app",
                table: "zone_market_rooms");

            migrationBuilder.DropColumn(
                name: "price_shares",
                schema: "app",
                table: "zone_market_rooms");

            migrationBuilder.DropColumn(
                name: "price_m2_shares",
                schema: "app",
                table: "zone_market");

            migrationBuilder.DropColumn(
                name: "price_shares",
                schema: "app",
                table: "zone_market");
        }
    }
}
