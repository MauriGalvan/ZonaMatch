using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ZonaMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TerritorialAndPoiModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "zonamatch");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:hstore", ",,")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "data_sources",
                schema: "zonamatch",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'1000', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<short>(type: "smallint", nullable: false),
                    publisher = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    license = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "poi_categories",
                schema: "zonamatch",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'1000', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    parent_id = table.Column<short>(type: "smallint", nullable: true),
                    sort_order = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_poi_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_poi_categories_poi_categories_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "zonamatch",
                        principalTable: "poi_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "territorial_units",
                schema: "zonamatch",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<short>(type: "smallint", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    geometry = table.Column<MultiPolygon>(type: "geometry(MultiPolygon,4326)", nullable: false),
                    area_m2 = table.Column<double>(type: "double precision", nullable: false),
                    data_source_id = table.Column<short>(type: "smallint", nullable: false),
                    external_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_date = table.Column<DateOnly>(type: "date", nullable: true),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_territorial_units", x => x.id);
                    table.ForeignKey(
                        name: "fk_territorial_units_data_sources_data_source_id",
                        column: x => x.data_source_id,
                        principalSchema: "zonamatch",
                        principalTable: "data_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_territorial_units_territorial_units_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "zonamatch",
                        principalTable: "territorial_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "poi_mapping_rules",
                schema: "zonamatch",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'1000', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    data_source_id = table.Column<short>(type: "smallint", nullable: false),
                    attribute = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    category_id = table.Column<short>(type: "smallint", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_poi_mapping_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_poi_mapping_rules_data_sources_data_source_id",
                        column: x => x.data_source_id,
                        principalSchema: "zonamatch",
                        principalTable: "data_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_poi_mapping_rules_poi_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "zonamatch",
                        principalTable: "poi_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "points_of_interest",
                schema: "zonamatch",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category_id = table.Column<short>(type: "smallint", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    location = table.Column<Point>(type: "geometry(Point,4326)", nullable: false),
                    footprint = table.Column<Geometry>(type: "geometry(Geometry,4326)", nullable: true),
                    ownership = table.Column<short>(type: "smallint", nullable: false),
                    attributes = table.Column<string>(type: "jsonb", nullable: false),
                    data_source_id = table.Column<short>(type: "smallint", nullable: false),
                    external_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_date = table.Column<DateOnly>(type: "date", nullable: true),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    canonical_poi_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_points_of_interest", x => x.id);
                    table.ForeignKey(
                        name: "fk_points_of_interest_data_sources_data_source_id",
                        column: x => x.data_source_id,
                        principalSchema: "zonamatch",
                        principalTable: "data_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_points_of_interest_poi_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "zonamatch",
                        principalTable: "poi_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_points_of_interest_points_of_interest_canonical_poi_id",
                        column: x => x.canonical_poi_id,
                        principalSchema: "zonamatch",
                        principalTable: "points_of_interest",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "territorial_indicators",
                schema: "zonamatch",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    territorial_unit_id = table.Column<long>(type: "bigint", nullable: false),
                    indicator_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    data_source_id = table.Column<short>(type: "smallint", nullable: false),
                    reference_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_territorial_indicators", x => x.id);
                    table.ForeignKey(
                        name: "fk_territorial_indicators_data_sources_data_source_id",
                        column: x => x.data_source_id,
                        principalSchema: "zonamatch",
                        principalTable: "data_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_territorial_indicators_territorial_units_territorial_unit_id",
                        column: x => x.territorial_unit_id,
                        principalSchema: "zonamatch",
                        principalTable: "territorial_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "zones",
                schema: "zonamatch",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    territorial_unit_id = table.Column<long>(type: "bigint", nullable: false),
                    slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    parent_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_zones", x => x.id);
                    table.ForeignKey(
                        name: "fk_zones_territorial_units_territorial_unit_id",
                        column: x => x.territorial_unit_id,
                        principalSchema: "zonamatch",
                        principalTable: "territorial_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "zone_adjacencies",
                schema: "zonamatch",
                columns: table => new
                {
                    zone_id = table.Column<int>(type: "integer", nullable: false),
                    neighbor_zone_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_zone_adjacencies", x => new { x.zone_id, x.neighbor_zone_id });
                    table.ForeignKey(
                        name: "fk_zone_adjacencies_zones_neighbor_zone_id",
                        column: x => x.neighbor_zone_id,
                        principalSchema: "zonamatch",
                        principalTable: "zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_zone_adjacencies_zones_zone_id",
                        column: x => x.zone_id,
                        principalSchema: "zonamatch",
                        principalTable: "zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "zonamatch",
                table: "data_sources",
                columns: new[] { "id", "code", "kind", "license", "name", "publisher", "url" },
                values: new object[,]
                {
                    { (short)1, "zm-comunas", (short)1, "CC BY 2.5 AR", "Comunas de CABA", "GCBA · Buenos Aires Data", "https://data.buenosaires.gob.ar/dataset/comunas" },
                    { (short)2, "zm-partidos", (short)1, "CC BY 4.0", "Partidos de la Provincia de Buenos Aires", "Datos Abiertos PBA", "https://catalogo.datos.gba.gob.ar/" },
                    { (short)3, "zm-radios", (short)1, "CC BY 4.0", "Radios censales 2022", "INDEC", "https://www.indec.gob.ar/indec/web/Institucional-Indec-Codgeo" },
                    { (short)4, "zm-escuelas", (short)1, "CC BY 4.0", "Padrón de establecimientos educativos", "DGCyE PBA", "https://abc.gob.ar/" },
                    { (short)5, "osm", (short)2, "ODbL 1.0", "OpenStreetMap", "OpenStreetMap contributors", "https://www.openstreetmap.org/copyright" },
                    { (short)6, "ba-barrios", (short)1, "CC BY 2.5 AR", "Barrios de CABA", "GCBA · Buenos Aires Data", "https://data.buenosaires.gob.ar/dataset/barrios" },
                    { (short)7, "community", (short)2, null, "Aportes de vecinos", "Comunidad ZonaMatch", null }
                });

            migrationBuilder.InsertData(
                schema: "zonamatch",
                table: "poi_categories",
                columns: new[] { "id", "code", "name", "parent_id", "sort_order" },
                values: new object[,]
                {
                    { (short)1, "transporte", "Transporte", null, (short)1 },
                    { (short)2, "educacion", "Educación", null, (short)2 },
                    { (short)3, "salud", "Salud", null, (short)3 },
                    { (short)4, "comercios_servicios", "Comercios y servicios", null, (short)4 },
                    { (short)5, "deporte", "Deporte", null, (short)5 },
                    { (short)6, "espacios_verdes", "Espacios verdes", null, (short)6 },
                    { (short)7, "cultura_gastronomia", "Cultura y gastronomía", null, (short)7 },
                    { (short)101, "transporte.estacion_tren", "Estación de tren", (short)1, (short)1 },
                    { (short)102, "transporte.subte", "Estación de subte", (short)1, (short)2 },
                    { (short)103, "transporte.parada_colectivo", "Parada de colectivo", (short)1, (short)3 },
                    { (short)104, "transporte.premetro", "Parada de premetro", (short)1, (short)4 },
                    { (short)201, "educacion.jardin", "Jardín", (short)2, (short)1 },
                    { (short)202, "educacion.primaria", "Escuela primaria", (short)2, (short)2 },
                    { (short)203, "educacion.secundaria", "Escuela secundaria", (short)2, (short)3 },
                    { (short)204, "educacion.escuela", "Escuela (nivel sin informar)", (short)2, (short)4 },
                    { (short)205, "educacion.superior", "Universidad o instituto superior", (short)2, (short)5 },
                    { (short)301, "salud.hospital", "Hospital", (short)3, (short)1 },
                    { (short)302, "salud.clinica", "Clínica o consultorio", (short)3, (short)2 },
                    { (short)303, "salud.centro_salud", "Centro de salud (CESAC / CAPS)", (short)3, (short)3 },
                    { (short)401, "comercios_servicios.supermercado", "Supermercado", (short)4, (short)1 },
                    { (short)402, "comercios_servicios.farmacia", "Farmacia", (short)4, (short)2 },
                    { (short)403, "comercios_servicios.banco", "Banco", (short)4, (short)3 },
                    { (short)501, "deporte.gimnasio", "Gimnasio", (short)5, (short)1 },
                    { (short)502, "deporte.club", "Club o polideportivo", (short)5, (short)2 },
                    { (short)503, "deporte.natatorio", "Natatorio", (short)5, (short)3 },
                    { (short)504, "deporte.cancha", "Cancha", (short)5, (short)4 },
                    { (short)601, "espacios_verdes.plaza_parque", "Plaza o parque", (short)6, (short)1 },
                    { (short)602, "espacios_verdes.reserva", "Reserva natural", (short)6, (short)2 },
                    { (short)701, "cultura_gastronomia.teatro", "Teatro", (short)7, (short)1 },
                    { (short)702, "cultura_gastronomia.museo", "Museo", (short)7, (short)2 },
                    { (short)703, "cultura_gastronomia.cine", "Cine", (short)7, (short)3 },
                    { (short)704, "cultura_gastronomia.restaurante", "Restaurante", (short)7, (short)4 },
                    { (short)705, "cultura_gastronomia.cafe_bar", "Café o bar", (short)7, (short)5 }
                });

            migrationBuilder.InsertData(
                schema: "zonamatch",
                table: "poi_mapping_rules",
                columns: new[] { "id", "attribute", "category_id", "data_source_id", "priority", "value" },
                values: new object[,]
                {
                    { 1, "station", (short)102, (short)5, 5, "subway" },
                    { 2, "railway", (short)102, (short)5, 10, "subway_entrance" },
                    { 3, "railway", (short)101, (short)5, 10, "station" },
                    { 4, "railway", (short)101, (short)5, 10, "halt" },
                    { 5, "highway", (short)103, (short)5, 10, "bus_stop" },
                    { 6, "railway", (short)104, (short)5, 10, "tram_stop" },
                    { 10, "amenity", (short)201, (short)5, 10, "kindergarten" },
                    { 11, "amenity", (short)204, (short)5, 10, "school" },
                    { 12, "amenity", (short)205, (short)5, 10, "college" },
                    { 13, "amenity", (short)205, (short)5, 10, "university" },
                    { 20, "healthcare", (short)303, (short)5, 5, "centre" },
                    { 21, "amenity", (short)301, (short)5, 10, "hospital" },
                    { 22, "amenity", (short)302, (short)5, 10, "clinic" },
                    { 23, "amenity", (short)302, (short)5, 10, "doctors" },
                    { 30, "shop", (short)401, (short)5, 10, "supermarket" },
                    { 31, "amenity", (short)402, (short)5, 10, "pharmacy" },
                    { 32, "amenity", (short)403, (short)5, 10, "bank" },
                    { 40, "sport", (short)503, (short)5, 5, "swimming" },
                    { 41, "leisure", (short)501, (short)5, 10, "fitness_centre" },
                    { 42, "leisure", (short)502, (short)5, 10, "sports_centre" },
                    { 43, "leisure", (short)504, (short)5, 10, "pitch" },
                    { 50, "leisure", (short)601, (short)5, 10, "park" },
                    { 51, "leisure", (short)602, (short)5, 10, "nature_reserve" },
                    { 60, "amenity", (short)701, (short)5, 10, "theatre" },
                    { 61, "tourism", (short)702, (short)5, 10, "museum" },
                    { 62, "amenity", (short)703, (short)5, 10, "cinema" },
                    { 63, "amenity", (short)704, (short)5, 10, "restaurant" },
                    { 64, "amenity", (short)705, (short)5, 10, "cafe" },
                    { 65, "amenity", (short)705, (short)5, 10, "bar" },
                    { 100, "nivel", (short)201, (short)4, 10, "Nivel Inicial" },
                    { 101, "nivel", (short)202, (short)4, 10, "Nivel Primario" },
                    { 102, "nivel", (short)203, (short)4, 10, "Nivel Secundario" },
                    { 103, "nivel", (short)205, (short)4, 10, "Nivel Superior" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_data_sources_code",
                schema: "zonamatch",
                table: "data_sources",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_poi_categories_code",
                schema: "zonamatch",
                table: "poi_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_poi_categories_parent_id",
                schema: "zonamatch",
                table: "poi_categories",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_poi_mapping_rules_category_id",
                schema: "zonamatch",
                table: "poi_mapping_rules",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_poi_mapping_rules_data_source_id_attribute_value",
                schema: "zonamatch",
                table: "poi_mapping_rules",
                columns: new[] { "data_source_id", "attribute", "value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_points_of_interest_canonical_poi_id",
                schema: "zonamatch",
                table: "points_of_interest",
                column: "canonical_poi_id");

            migrationBuilder.CreateIndex(
                name: "ix_points_of_interest_category_id",
                schema: "zonamatch",
                table: "points_of_interest",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_points_of_interest_data_source_id_external_id",
                schema: "zonamatch",
                table: "points_of_interest",
                columns: new[] { "data_source_id", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_points_of_interest_location",
                schema: "zonamatch",
                table: "points_of_interest",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_territorial_indicators_data_source_id",
                schema: "zonamatch",
                table: "territorial_indicators",
                column: "data_source_id");

            migrationBuilder.CreateIndex(
                name: "ix_territorial_indicators_territorial_unit_id_indicator_code_d~",
                schema: "zonamatch",
                table: "territorial_indicators",
                columns: new[] { "territorial_unit_id", "indicator_code", "data_source_id", "reference_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_territorial_units_data_source_id_external_id",
                schema: "zonamatch",
                table: "territorial_units",
                columns: new[] { "data_source_id", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_territorial_units_geometry",
                schema: "zonamatch",
                table: "territorial_units",
                column: "geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_territorial_units_parent_id",
                schema: "zonamatch",
                table: "territorial_units",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_territorial_units_type_code",
                schema: "zonamatch",
                table: "territorial_units",
                columns: new[] { "type", "code" },
                unique: true,
                filter: "code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_territorial_units_type_normalized_name",
                schema: "zonamatch",
                table: "territorial_units",
                columns: new[] { "type", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "ix_zone_adjacencies_neighbor_zone_id",
                schema: "zonamatch",
                table: "zone_adjacencies",
                column: "neighbor_zone_id");

            migrationBuilder.CreateIndex(
                name: "ix_zones_slug",
                schema: "zonamatch",
                table: "zones",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_zones_territorial_unit_id",
                schema: "zonamatch",
                table: "zones",
                column: "territorial_unit_id",
                unique: true);

            // las búsquedas por radio usan ST_DWithin sobre geography (metros): índice por expresión
            migrationBuilder.Sql(
                "CREATE INDEX ix_points_of_interest_location_geography ON zonamatch.points_of_interest USING gist ((location::geography));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS zonamatch.ix_points_of_interest_location_geography;");

            migrationBuilder.DropTable(
                name: "poi_mapping_rules",
                schema: "zonamatch");

            migrationBuilder.DropTable(
                name: "points_of_interest",
                schema: "zonamatch");

            migrationBuilder.DropTable(
                name: "territorial_indicators",
                schema: "zonamatch");

            migrationBuilder.DropTable(
                name: "zone_adjacencies",
                schema: "zonamatch");

            migrationBuilder.DropTable(
                name: "poi_categories",
                schema: "zonamatch");

            migrationBuilder.DropTable(
                name: "zones",
                schema: "zonamatch");

            migrationBuilder.DropTable(
                name: "territorial_units",
                schema: "zonamatch");

            migrationBuilder.DropTable(
                name: "data_sources",
                schema: "zonamatch");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:hstore", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");
        }
    }
}
