using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ZonaMatch.Infrastructure.Data;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    // Written by hand (no .Designer.cs file), like AddReviewModeration: the model snapshot already includes the new column.
    // Check with: dotnet ef migrations has-pending-model-changes --project ZonaMatch.Infrastructure --startup-project ZonaMatch.Api
    //
    // Links a "review to moderate" notification to its review, so resolving or deleting the review can clear it from the
    // bell of every administrator. Notifications created before this migration keep a null review_id and are not cleared.
    [DbContext(typeof(ZonaMatchDbContext))]
    [Migration("20261108210000_AddNotificationReviewId")]
    public class AddNotificationReviewId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "review_id",
                schema: "app",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_review_id",
                schema: "app",
                table: "notifications",
                column: "review_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notifications_review_id",
                schema: "app",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "review_id",
                schema: "app",
                table: "notifications");
        }
    }
}
