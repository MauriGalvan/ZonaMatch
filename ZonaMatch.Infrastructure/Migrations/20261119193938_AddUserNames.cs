using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "first_name",
                schema: "app",
                table: "usuarios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_name",
                schema: "app",
                table: "usuarios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "first_name",
                schema: "app",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "last_name",
                schema: "app",
                table: "usuarios");
        }
    }
}
