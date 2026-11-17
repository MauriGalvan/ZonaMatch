using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisTrash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_eliminacion",
                schema: "app",
                table: "analisis",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_analisis_fecha_eliminacion",
                schema: "app",
                table: "analisis",
                column: "fecha_eliminacion");

            migrationBuilder.CreateIndex(
                name: "IX_analisis_usuario_id_fecha_eliminacion",
                schema: "app",
                table: "analisis",
                columns: new[] { "usuario_id", "fecha_eliminacion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_analisis_fecha_eliminacion",
                schema: "app",
                table: "analisis");

            migrationBuilder.DropIndex(
                name: "IX_analisis_usuario_id_fecha_eliminacion",
                schema: "app",
                table: "analisis");

            migrationBuilder.DropColumn(
                name: "fecha_eliminacion",
                schema: "app",
                table: "analisis");
        }
    }
}
