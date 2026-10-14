using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ZonaMatch.Infrastructure.Data;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations;

[DbContext(typeof(ZonaMatchDbContext))]
[Migration("20261014190000_GuardarPuntajeEnGrupoEnAnalisis")]
public partial class GuardarPuntajeEnGrupoEnAnalisis : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "puntajes_en_grupo_json",
            schema: "app",
            table: "analisis",
            type: "jsonb",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "puntajes_en_grupo_json",
            schema: "app",
            table: "analisis");
    }
}
