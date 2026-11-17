using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestorAnalysisContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_contextos_tipo",
                schema: "app",
                table: "contextos");

            migrationBuilder.AddCheckConstraint(
                name: "ck_contextos_tipo",
                schema: "app",
                table: "contextos",
                sql: "tipo IN ('perfil', 'analisis', 'inversor', 'analisis-inversor')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_contextos_tipo",
                schema: "app",
                table: "contextos");

            migrationBuilder.AddCheckConstraint(
                name: "ck_contextos_tipo",
                schema: "app",
                table: "contextos",
                sql: "tipo IN ('perfil', 'analisis', 'inversor')");
        }
    }
}
