using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ZonaMatch.Infrastructure.Data;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    // Written by hand (no .Designer.cs file): the model snapshot already includes the new column.
    // Check with: dotnet ef migrations has-pending-model-changes --project ZonaMatch.Infrastructure --startup-project ZonaMatch.Api
    [DbContext(typeof(ZonaMatchDbContext))]
    [Migration("20261106124500_AddReviewImages")]
    public class AddReviewImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "imagenes",
                schema: "app",
                table: "resenas",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "imagenes",
                schema: "app",
                table: "resenas");
        }
    }
}
