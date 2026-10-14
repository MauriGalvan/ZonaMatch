using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRelacionUsuarioParticipante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "usuario_id",
                schema: "app",
                table: "Participant",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Participant_usuario_id",
                schema: "app",
                table: "Participant",
                column: "usuario_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Participant_usuarios_usuario_id",
                schema: "app",
                table: "Participant",
                column: "usuario_id",
                principalSchema: "app",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Participant_usuarios_usuario_id",
                schema: "app",
                table: "Participant");

            migrationBuilder.DropIndex(
                name: "IX_Participant_usuario_id",
                schema: "app",
                table: "Participant");

            migrationBuilder.DropColumn(
                name: "usuario_id",
                schema: "app",
                table: "Participant");
        }
    }
}
