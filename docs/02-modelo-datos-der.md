# Modelo de datos de zonas y unidades (PBI 3 · tarea 3.2)

Este modelo es el **contrato interno** de ZonaMatch. Las fuentes externas (base compartida
`zm.*`, OpenStreetMap, archivos oficiales) solo se leen durante la ingesta. Todo lo que
consume la API sale de estas tablas, que viven en el esquema `zonamatch`, siempre en
**EPSG:4326**.

## DER

```mermaid
erDiagram
    data_sources ||--o{ territorial_units : "origina"
    data_sources ||--o{ points_of_interest : "origina"
    data_sources ||--o{ poi_mapping_rules : "se traduce con"
    data_sources ||--o{ territorial_indicators : "origina"

    territorial_units ||--o{ territorial_units : "contiene (parent_id)"
    territorial_units ||--o| zones : "se analiza como"
    territorial_units ||--o{ territorial_indicators : "tiene"

    zones ||--o{ zone_adjacencies : "zona"
    zones ||--o{ zone_adjacencies : "vecina"

    poi_categories ||--o{ poi_categories : "agrupa (parent_id)"
    poi_categories ||--o{ poi_mapping_rules : "destino"
    poi_categories ||--o{ points_of_interest : "clasifica"
    points_of_interest ||--o{ points_of_interest : "duplicado de (canonical_poi_id)"

    data_sources {
        smallint id PK
        varchar code UK "osm, zm-escuelas, zm-radios, ..."
        varchar name
        varchar publisher
        varchar url
        varchar license
        smallint kind "Official | OpenData | Community"
    }
    territorial_units {
        bigint id PK
        smallint type "Jurisdiction | Comuna | Partido | Barrio | Localidad | CensusTract"
        varchar code "código oficial (INDEC, comuna, partido)"
        varchar name
        varchar normalized_name "sin tildes, mayúsculas"
        bigint parent_id FK
        geometry geometry "MultiPolygon 4326, GiST"
        double area_m2
        smallint data_source_id FK
        varchar external_id "UK con data_source_id"
        date source_date
        timestamptz imported_at
    }
    zones {
        int id PK
        bigint territorial_unit_id FK,UK "barrio o localidad"
        varchar slug UK "ramos-mejia-la-matanza"
        varchar name "Ramos Mejía"
        varchar parent_name "La Matanza | CABA"
    }
    zone_adjacencies {
        int zone_id PK,FK
        int neighbor_zone_id PK,FK
    }
    territorial_indicators {
        bigint id PK
        bigint territorial_unit_id FK
        varchar indicator_code "population, crime_rate, ..."
        numeric value
        varchar unit
        smallint data_source_id FK
        date reference_date
    }
    poi_categories {
        smallint id PK
        varchar code UK "educacion, educacion.primaria"
        varchar name
        smallint parent_id FK
        smallint sort_order
    }
    poi_mapping_rules {
        int id PK
        smallint data_source_id FK
        varchar attribute "amenity | nivel"
        varchar value "school | Nivel Primario"
        smallint category_id FK "subcategoría destino"
        int priority
    }
    points_of_interest {
        bigint id PK
        smallint category_id FK
        varchar name
        varchar normalized_name
        varchar address
        geometry location "Point 4326, GiST"
        geometry footprint "Geometry 4326, opcional"
        smallint ownership "Unknown | Public | Private"
        jsonb attributes "atributos propios de la fuente"
        smallint data_source_id FK
        varchar external_id "UK con data_source_id"
        date source_date
        timestamptz imported_at
        bigint canonical_poi_id FK
    }
```

## Decisiones de diseño

**Una tabla jerárquica para todas las unidades.** Barrio, localidad, comuna, partido y
radio comparten estructura (nombre, código, polígono, procedencia). Separarlas en cinco
tablas duplicaría índices y consultas. El tipo y el `parent_id` arman la jerarquía:

```text
Jurisdicción (CABA)          Jurisdicción (Buenos Aires)
└── Comuna                   └── Partido
    ├── Barrio                   ├── Localidad
    └── Radio censal             └── Radio censal
```

El radio censal cuelga de la comuna o el partido, que es lo que informa el INDEC. El barrio
o la localidad que lo contiene se resuelve espacialmente cuando hace falta (PBI 14).

**`zones` separada de `territorial_units`.** No toda unidad se analiza: solo barrios de
CABA y localidades de los 24 partidos. `zones` fija esa lista, guarda el nombre para
mostrar y un `slug` estable para URLs, análisis guardados y favoritos. Si mañana se analiza
por radio o por un polígono propio, cambia esta tabla y no el resto.

**Adyacencia materializada.** `zone_adjacencies` se calcula al construir las zonas
(`ST_Intersects` sobre el borde). Así la pantalla H05 lista aledañas sin cálculo espacial
en cada pedido (PBI 53a).

**Taxonomía propia de POIs.** `poi_categories` tiene dos niveles: las 7 categorías estándar
de las pantallas (Transporte, Educación, Salud, Comercios y servicios, Deporte, Espacios
verdes, Cultura y gastronomía) y sus subcategorías. Los POIs siempre apuntan a una
subcategoría. Cada fuente se traduce con `poi_mapping_rules`; agregar, por ejemplo,
veterinarias (PBI 69) es insertar una subcategoría y una regla `amenity = veterinary`.

**Punto representativo y huella.** `location` es siempre un punto (para radios de
búsqueda y marcadores). Si la fuente trae un polígono, se guarda en `footprint` y
`location` es un punto interior de ese polígono.

**Duplicados entre fuentes.** `canonical_poi_id` apunta al POI que se muestra. La
deduplicación compara subcategorías de la misma categoría, distancia (≤ 100 m) y nombre
normalizado. Gana la fuente oficial.

**Indicadores con trazabilidad.** `territorial_indicators` asocia valores a cualquier
unidad (zona, partido, radio). Si la zona no tiene un indicador, el servicio sube por
`parent_id` y marca el valor como **heredado** (PBI 21, 37a).

## Índices

| Tabla | Índice | Uso |
|---|---|---|
| `territorial_units` | GiST(`geometry`) | Punto en polígono, adyacencia |
| `territorial_units` | (`type`, `normalized_name`) | Búsqueda por nombre |
| `territorial_units` | (`type`, `code`) único parcial | Resolver padres por código oficial |
| `territorial_units` | (`data_source_id`, `external_id`) único | Reimportación idempotente (upsert) |
| `points_of_interest` | GiST(`location`) | POIs en un radio (`ST_DWithin` sobre `geography`) |
| `points_of_interest` | (`category_id`) | Filtro por capa |
| `points_of_interest` | (`data_source_id`, `external_id`) único | Reimportación idempotente |
| `territorial_indicators` | (`territorial_unit_id`, `indicator_code`) | Lectura y herencia |

El DDL se genera desde las migraciones de EF Core: [`db/ddl/zonamatch_schema.sql`](../db/ddl/zonamatch_schema.sql).
