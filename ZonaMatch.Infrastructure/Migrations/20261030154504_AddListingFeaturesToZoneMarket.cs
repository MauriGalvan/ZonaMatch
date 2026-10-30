using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddListingFeaturesToZoneMarket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "credit_eligible_share",
                schema: "app",
                table: "zone_market",
                type: "numeric(5,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "median_covered_m2",
                schema: "app",
                table: "zone_market",
                type: "numeric(8,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "median_total_m2",
                schema: "app",
                table: "zone_market",
                type: "numeric(8,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "owner_share",
                schema: "app",
                table: "zone_market",
                type: "numeric(5,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "pets_allowed_share",
                schema: "app",
                table: "zone_market",
                type: "numeric(5,4)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "credit_eligible_share",
                schema: "app",
                table: "zone_market");

            migrationBuilder.DropColumn(
                name: "median_covered_m2",
                schema: "app",
                table: "zone_market");

            migrationBuilder.DropColumn(
                name: "median_total_m2",
                schema: "app",
                table: "zone_market");

            migrationBuilder.DropColumn(
                name: "owner_share",
                schema: "app",
                table: "zone_market");

            migrationBuilder.DropColumn(
                name: "pets_allowed_share",
                schema: "app",
                table: "zone_market");
        }
    }
}
