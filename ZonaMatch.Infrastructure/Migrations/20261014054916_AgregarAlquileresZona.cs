using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAlquileresZona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alquileres_zona",
                schema: "app",
                columns: table => new
                {
                    zona_slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    alquiler_promedio = table.Column<decimal>(type: "numeric(12,0)", nullable: false),
                    propiedades_disponibles = table.Column<int>(type: "integer", nullable: false),
                    fuente = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    fecha_actualizacion = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alquileres_zona", x => x.zona_slug);
                    table.CheckConstraint("ck_alquileres_zona_promedio", "alquiler_promedio >= 0");
                    table.CheckConstraint("ck_alquileres_zona_propiedades", "propiedades_disponibles >= 0");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alquileres_zona",
                schema: "app");
        }
    }
}
