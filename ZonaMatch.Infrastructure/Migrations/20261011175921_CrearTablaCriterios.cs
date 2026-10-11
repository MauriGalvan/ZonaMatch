using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CrearTablaCriterios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "criterios",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    icono = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_criterios", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_criterios_orden",
                schema: "app",
                table: "criterios",
                column: "orden");

            // Seed del catalogo. SQL a mano y no HasData: una vez sembrada, la data es editable
            // en la base sin que la proxima migracion la pise (mismo criterio que geo/osm).
            migrationBuilder.Sql("""
                INSERT INTO app.criterios (id, nombre, descripcion, icono, orden, activo) VALUES
                    ('seguridad', 'Seguridad', 'Delitos y percepción de seguridad', 'pi pi-shield', 1, TRUE),
                    ('movilidad', 'Movilidad', 'Tiempo a tus lugares y transporte', 'pi pi-car', 2, TRUE),
                    ('espacios-verdes', 'Espacios verdes', 'Plazas y parques cercanos', 'pi pi-sun', 3, TRUE),
                    ('educacion', 'Educación', 'Escuelas y universidades', 'pi pi-graduation-cap', 4, TRUE),
                    ('salud', 'Salud', 'Hospitales y salitas', 'pi pi-heart', 5, TRUE)
                ON CONFLICT (id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "criterios",
                schema: "app");
        }
    }
}
