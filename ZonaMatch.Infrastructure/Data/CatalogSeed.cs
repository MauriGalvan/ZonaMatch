using ZonaMatch.Domain.Sources;

namespace ZonaMatch.Infrastructure.Data
{
    // catálogo inicial: fuentes, taxonomía estándar de POIs y reglas de traducción por fuente.
    // agregar una categoría o una fuente nueva es agregar filas, no código (PBI 12)
    public static class CatalogSeed
    {
        public static class Sources
        {
            public const short ZmComunas = 1;
            public const short ZmPartidos = 2;
            public const short ZmRadios = 3;
            public const short ZmEscuelas = 4;
            public const short Osm = 5;
            public const short BaBarrios = 6;
            public const short Community = 7;
        }

        public static readonly object[] DataSources =
        [
            Source(Sources.ZmComunas, "zm-comunas", "Comunas de CABA", DataSourceKind.Official, "GCBA · Buenos Aires Data", "https://data.buenosaires.gob.ar/dataset/comunas", "CC BY 2.5 AR"),
            Source(Sources.ZmPartidos, "zm-partidos", "Partidos de la Provincia de Buenos Aires", DataSourceKind.Official, "Datos Abiertos PBA", "https://catalogo.datos.gba.gob.ar/", "CC BY 4.0"),
            Source(Sources.ZmRadios, "zm-radios", "Radios censales 2022", DataSourceKind.Official, "INDEC", "https://www.indec.gob.ar/indec/web/Institucional-Indec-Codgeo", "CC BY 4.0"),
            Source(Sources.ZmEscuelas, "zm-escuelas", "Padrón de establecimientos educativos", DataSourceKind.Official, "DGCyE PBA", "https://abc.gob.ar/", "CC BY 4.0"),
            Source(Sources.Osm, "osm", "OpenStreetMap", DataSourceKind.Community, "OpenStreetMap contributors", "https://www.openstreetmap.org/copyright", "ODbL 1.0"),
            Source(Sources.BaBarrios, "ba-barrios", "Barrios de CABA", DataSourceKind.Official, "GCBA · Buenos Aires Data", "https://data.buenosaires.gob.ar/dataset/barrios", "CC BY 2.5 AR"),
            Source(Sources.Community, "community", "Aportes de vecinos", DataSourceKind.Community, "Comunidad ZonaMatch", null, null),
        ];

        public static readonly object[] Categories =
        [
            Category(1, "transporte", "Transporte", null, 1),
            Category(2, "educacion", "Educación", null, 2),
            Category(3, "salud", "Salud", null, 3),
            Category(4, "comercios_servicios", "Comercios y servicios", null, 4),
            Category(5, "deporte", "Deporte", null, 5),
            Category(6, "espacios_verdes", "Espacios verdes", null, 6),
            Category(7, "cultura_gastronomia", "Cultura y gastronomía", null, 7),

            Category(101, "transporte.estacion_tren", "Estación de tren", 1, 1),
            Category(102, "transporte.subte", "Estación de subte", 1, 2),
            Category(103, "transporte.parada_colectivo", "Parada de colectivo", 1, 3),
            Category(104, "transporte.premetro", "Parada de premetro", 1, 4),

            Category(201, "educacion.jardin", "Jardín", 2, 1),
            Category(202, "educacion.primaria", "Escuela primaria", 2, 2),
            Category(203, "educacion.secundaria", "Escuela secundaria", 2, 3),
            Category(204, "educacion.escuela", "Escuela (nivel sin informar)", 2, 4),
            Category(205, "educacion.superior", "Universidad o instituto superior", 2, 5),

            Category(301, "salud.hospital", "Hospital", 3, 1),
            Category(302, "salud.clinica", "Clínica o consultorio", 3, 2),
            Category(303, "salud.centro_salud", "Centro de salud (CESAC / CAPS)", 3, 3),

            Category(401, "comercios_servicios.supermercado", "Supermercado", 4, 1),
            Category(402, "comercios_servicios.farmacia", "Farmacia", 4, 2),
            Category(403, "comercios_servicios.banco", "Banco", 4, 3),

            Category(501, "deporte.gimnasio", "Gimnasio", 5, 1),
            Category(502, "deporte.club", "Club o polideportivo", 5, 2),
            Category(503, "deporte.natatorio", "Natatorio", 5, 3),
            Category(504, "deporte.cancha", "Cancha", 5, 4),

            Category(601, "espacios_verdes.plaza_parque", "Plaza o parque", 6, 1),
            Category(602, "espacios_verdes.reserva", "Reserva natural", 6, 2),

            Category(701, "cultura_gastronomia.teatro", "Teatro", 7, 1),
            Category(702, "cultura_gastronomia.museo", "Museo", 7, 2),
            Category(703, "cultura_gastronomia.cine", "Cine", 7, 3),
            Category(704, "cultura_gastronomia.restaurante", "Restaurante", 7, 4),
            Category(705, "cultura_gastronomia.cafe_bar", "Café o bar", 7, 5),
        ];

        // menor prioridad gana cuando un elemento cumple más de una regla
        public static readonly object[] Rules =
        [
            Rule(1, Sources.Osm, "station", "subway", 102, 5),
            Rule(2, Sources.Osm, "railway", "subway_entrance", 102, 10),
            Rule(3, Sources.Osm, "railway", "station", 101, 10),
            Rule(4, Sources.Osm, "railway", "halt", 101, 10),
            Rule(5, Sources.Osm, "highway", "bus_stop", 103, 10),
            Rule(6, Sources.Osm, "railway", "tram_stop", 104, 10),

            Rule(10, Sources.Osm, "amenity", "kindergarten", 201, 10),
            Rule(11, Sources.Osm, "amenity", "school", 204, 10),
            Rule(12, Sources.Osm, "amenity", "college", 205, 10),
            Rule(13, Sources.Osm, "amenity", "university", 205, 10),

            Rule(20, Sources.Osm, "healthcare", "centre", 303, 5),
            Rule(21, Sources.Osm, "amenity", "hospital", 301, 10),
            Rule(22, Sources.Osm, "amenity", "clinic", 302, 10),
            Rule(23, Sources.Osm, "amenity", "doctors", 302, 10),

            Rule(30, Sources.Osm, "shop", "supermarket", 401, 10),
            Rule(31, Sources.Osm, "amenity", "pharmacy", 402, 10),
            Rule(32, Sources.Osm, "amenity", "bank", 403, 10),

            Rule(40, Sources.Osm, "sport", "swimming", 503, 5),
            Rule(41, Sources.Osm, "leisure", "fitness_centre", 501, 10),
            Rule(42, Sources.Osm, "leisure", "sports_centre", 502, 10),
            Rule(43, Sources.Osm, "leisure", "pitch", 504, 10),

            Rule(50, Sources.Osm, "leisure", "park", 601, 10),
            Rule(51, Sources.Osm, "leisure", "nature_reserve", 602, 10),

            Rule(60, Sources.Osm, "amenity", "theatre", 701, 10),
            Rule(61, Sources.Osm, "tourism", "museum", 702, 10),
            Rule(62, Sources.Osm, "amenity", "cinema", 703, 10),
            Rule(63, Sources.Osm, "amenity", "restaurant", 704, 10),
            Rule(64, Sources.Osm, "amenity", "cafe", 705, 10),
            Rule(65, Sources.Osm, "amenity", "bar", 705, 10),

            // el padrón distingue el nivel, OSM no: misma categoría raíz, distinta subcategoría
            Rule(100, Sources.ZmEscuelas, "nivel", "Nivel Inicial", 201, 10),
            Rule(101, Sources.ZmEscuelas, "nivel", "Nivel Primario", 202, 10),
            Rule(102, Sources.ZmEscuelas, "nivel", "Nivel Secundario", 203, 10),
            Rule(103, Sources.ZmEscuelas, "nivel", "Nivel Superior", 205, 10),
        ];

        private static object Source(short id, string code, string name, DataSourceKind kind, string publisher, string? url, string? license) =>
            new { Id = id, Code = code, Name = name, Kind = kind, Publisher = publisher, Url = url, License = license };

        private static object Category(short id, string code, string name, short? parentId, short sortOrder) =>
            new { Id = id, Code = code, Name = name, ParentId = parentId, SortOrder = sortOrder };

        private static object Rule(int id, short dataSourceId, string attribute, string value, short categoryId, int priority) =>
            new { Id = id, DataSourceId = dataSourceId, Attribute = attribute, Value = value, CategoryId = categoryId, Priority = priority };
    }
}
