using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestorProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_contextos_tipo",
                schema: "app",
                table: "contextos");

            migrationBuilder.AddColumn<string>(
                name: "acceso_inversor",
                schema: "app",
                table: "usuarios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "acceso_inversor_habilitado",
                schema: "app",
                table: "usuarios",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "acceso_inversor_solicitado",
                schema: "app",
                table: "usuarios",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "perfil_hogar",
                schema: "app",
                table: "criterios",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "perfil_inversor",
                schema: "app",
                table: "criterios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_usuarios_acceso_inversor",
                schema: "app",
                table: "usuarios",
                sql: "acceso_inversor IN ('solicitado', 'habilitado')");

            migrationBuilder.CreateIndex(
                name: "ux_contextos_inversor_usuario",
                schema: "app",
                table: "contextos",
                column: "usuario_id",
                unique: true,
                filter: "tipo = 'inversor'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_contextos_tipo",
                schema: "app",
                table: "contextos",
                sql: "tipo IN ('perfil', 'analisis', 'inversor')");

            // Investor profile criteria; safety and mobility are shared with the household profile
            migrationBuilder.Sql("UPDATE app.criterios SET perfil_inversor = true WHERE id IN ('seguridad', 'movilidad');");
            migrationBuilder.Sql("""
                INSERT INTO app.criterios (id, nombre, descripcion, icono, orden, activo, perfil_hogar, perfil_inversor) VALUES
                    ('rentabilidad', 'Rentabilidad', 'Alquiler anual frente al precio de compra', 'pi pi-percentage', 20, true, false, true),
                    ('oportunidad', 'Precio frente a la zona', 'Precio del m² comparado con su partido o comuna', 'pi pi-tag', 21, true, false, true),
                    ('liquidez', 'Liquidez', 'Movimiento del mercado de venta para revender', 'pi pi-sync', 22, true, false, true),
                    ('demanda-alquiler', 'Demanda de alquiler', 'Hogares que alquilan en la zona', 'pi pi-key', 23, true, false, true)
                ON CONFLICT (id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM app.contexto_criterios WHERE criterio_id IN ('rentabilidad', 'oportunidad', 'liquidez', 'demanda-alquiler');");
            migrationBuilder.Sql("DELETE FROM app.criterios WHERE id IN ('rentabilidad', 'oportunidad', 'liquidez', 'demanda-alquiler');");
            migrationBuilder.Sql("DELETE FROM app.contextos WHERE tipo = 'inversor';");

            migrationBuilder.DropCheckConstraint(
                name: "ck_usuarios_acceso_inversor",
                schema: "app",
                table: "usuarios");

            migrationBuilder.DropIndex(
                name: "ux_contextos_inversor_usuario",
                schema: "app",
                table: "contextos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_contextos_tipo",
                schema: "app",
                table: "contextos");

            migrationBuilder.DropColumn(
                name: "acceso_inversor",
                schema: "app",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "acceso_inversor_habilitado",
                schema: "app",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "acceso_inversor_solicitado",
                schema: "app",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "perfil_hogar",
                schema: "app",
                table: "criterios");

            migrationBuilder.DropColumn(
                name: "perfil_inversor",
                schema: "app",
                table: "criterios");

            migrationBuilder.AddCheckConstraint(
                name: "ck_contextos_tipo",
                schema: "app",
                table: "contextos",
                sql: "tipo IN ('perfil', 'analisis')");
        }
    }
}
