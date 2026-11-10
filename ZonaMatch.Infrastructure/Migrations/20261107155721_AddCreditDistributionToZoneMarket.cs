using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditDistributionToZoneMarket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal[]>(
                name: "credit_prices",
                schema: "app",
                table: "zone_market",
                type: "numeric[]",
                nullable: true);

            migrationBuilder.AddColumn<decimal[]>(
                name: "credit_shares",
                schema: "app",
                table: "zone_market",
                type: "numeric[]",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "credit_prices",
                schema: "app",
                table: "zone_market");

            migrationBuilder.DropColumn(
                name: "credit_shares",
                schema: "app",
                table: "zone_market");
        }
    }
}
