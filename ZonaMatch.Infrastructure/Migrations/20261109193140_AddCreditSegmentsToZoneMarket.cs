using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditSegmentsToZoneMarket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "credit_listings",
                schema: "app",
                table: "zone_market_rooms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal[]>(
                name: "credit_prices",
                schema: "app",
                table: "zone_market_rooms",
                type: "numeric[]",
                nullable: true);

            migrationBuilder.AddColumn<decimal[]>(
                name: "credit_shares",
                schema: "app",
                table: "zone_market_rooms",
                type: "numeric[]",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "credit_listings",
                schema: "app",
                table: "zone_market",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "credit_listings",
                schema: "app",
                table: "zone_market_rooms");

            migrationBuilder.DropColumn(
                name: "credit_prices",
                schema: "app",
                table: "zone_market_rooms");

            migrationBuilder.DropColumn(
                name: "credit_shares",
                schema: "app",
                table: "zone_market_rooms");

            migrationBuilder.DropColumn(
                name: "credit_listings",
                schema: "app",
                table: "zone_market");
        }
    }
}
