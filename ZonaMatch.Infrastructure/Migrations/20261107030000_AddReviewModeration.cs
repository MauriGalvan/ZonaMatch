using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ZonaMatch.Infrastructure.Data;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    // Written by hand (no .Designer.cs file), like AddReviewImages: the model snapshot already includes the new columns.
    // Check with: dotnet ef migrations has-pending-model-changes --project ZonaMatch.Infrastructure --startup-project ZonaMatch.Api
    //
    // The reviews that exist today were already public, so they are backfilled as approved and published. The application
    // always sets the status of new reviews (pending), so the "Approved" column default only serves this backfill.
    [DbContext(typeof(ZonaMatchDbContext))]
    [Migration("20261107030000_AddReviewModeration")]
    public class AddReviewModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "estado",
                schema: "app",
                table: "resenas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Approved");

            migrationBuilder.AddColumn<string>(
                name: "motivo_rechazo",
                schema: "app",
                table: "resenas",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_resolucion",
                schema: "app",
                table: "resenas",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "resuelta_por_id",
                schema: "app",
                table: "resenas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_publicacion",
                schema: "app",
                table: "resenas",
                type: "timestamptz",
                nullable: true);

            // Existing reviews were published when they were created, so editing one later does not announce it again.
            migrationBuilder.Sql("UPDATE app.resenas SET fecha_publicacion = fecha_creacion;");

            migrationBuilder.CreateIndex(
                name: "IX_resenas_resuelta_por_id",
                schema: "app",
                table: "resenas",
                column: "resuelta_por_id");

            migrationBuilder.AddForeignKey(
                name: "FK_resenas_usuarios_resuelta_por_id",
                schema: "app",
                table: "resenas",
                column: "resuelta_por_id",
                principalSchema: "app",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_resenas_usuarios_resuelta_por_id",
                schema: "app",
                table: "resenas");

            migrationBuilder.DropIndex(
                name: "IX_resenas_resuelta_por_id",
                schema: "app",
                table: "resenas");

            migrationBuilder.DropColumn(name: "fecha_publicacion", schema: "app", table: "resenas");
            migrationBuilder.DropColumn(name: "resuelta_por_id", schema: "app", table: "resenas");
            migrationBuilder.DropColumn(name: "fecha_resolucion", schema: "app", table: "resenas");
            migrationBuilder.DropColumn(name: "motivo_rechazo", schema: "app", table: "resenas");
            migrationBuilder.DropColumn(name: "estado", schema: "app", table: "resenas");
        }
    }
}
