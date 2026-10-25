using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_resultado",
                schema: "app",
                table: "analisis",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resultado_json",
                schema: "app",
                table: "analisis",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fecha_resultado",
                schema: "app",
                table: "analisis");

            migrationBuilder.DropColumn(
                name: "resultado_json",
                schema: "app",
                table: "analisis");
        }
    }
}
