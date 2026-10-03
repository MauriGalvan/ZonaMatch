# Relevamiento de unidades territoriales y puntos de interés (PBI 3 · tarea 3.1)

Alcance geográfico del producto: **CABA + los 24 partidos del conurbano** (AMBA).
Este documento releva qué datos tenemos, cómo vienen y qué decisiones de modelo se
desprenden. El modelo resultante está en [02-modelo-datos-der.md](02-modelo-datos-der.md).

## 1. Unidades territoriales que usan las pantallas

| Unidad | Dónde aparece | Ejemplo | Comentario |
|---|---|---|---|
| Jurisdicción | Encabezado de zona | `CABA`, `Buenos Aires` | Primer nivel. |
| Comuna (CABA) | H22 "CABA · Comuna 10" | Comuna 10 | Segundo nivel en CABA. |
| Partido (PBA) | H14 "Localidad del partido de La Matanza" | La Matanza | Segundo nivel en PBA. |
| Barrio (CABA) | Ranking, zonas agregadas | Villa Luro, Liniers | **Unidad de análisis** en CABA. |
| Localidad (GBA) | Ranking, zona inicial, aledañas | Ramos Mejía, Haedo, San Justo | **Unidad de análisis** en GBA (revisión de sprints, PBI 3). |
| Radio censal | Contextualización (PBI 14), demografía (PBI 22a) | `020910503` | Unidad estadística mínima del INDEC. |

Conclusión: la "zona" que se analiza, rankea y guarda es un **barrio** en CABA o una
**localidad** en el GBA. Comuna/partido y radio censal son unidades de contexto y de
herencia de datos (por ejemplo, seguridad heredada del partido, PBI 21).

## 2. Fuentes relevadas

### 2.1 Base compartida `BaseDatos-ZonaMatch/zonamatch.sql` (esquema `zm`)

| Tabla | Filas | Geometría | Clave | Observaciones |
|---|---|---|---|---|
| `zm.comunas` | 15 | `MultiPolygon, 4326` | `key` (`COMUNA 6`) | `censo_codigo` usa cero a la izquierda (`COMUNA 06`). |
| `zm.partidos` | 143 | `MultiPolygon, 4326` | `key` (`LA MATANZA`) | **Toda la provincia**; `censo_codigo` de 3 dígitos (`427`). Nombre en mayúsculas en `key` y en formato título en `nombre`. |
| `zm.radios` | 27.721 | `MultiPolygon, 4326` | `id` (9 dígitos INDEC) | `origen` = `caba` (3.820) o `pba` (23.901). En PBA, `depto` = `partidos.censo_codigo`; en CABA, `comuna` = `comunas.censo_codigo`. |
| `zm.municipio_alias` | 8 | — | `municipio_nombre` | Sinónimos de partidos (`25 de Mayo` → `VEINTICINCO DE MAYO`). |
| `zm.escuelas` | 21.589 | `Point, 4326` | `clave_natural` | Padrón oficial de establecimientos educativos. **Solo PBA** (no hay escuelas de CABA). 48 columnas. |

Cruces validados sobre el dump:

- Los 135 `depto` distintos de radios PBA existen en `partidos.censo_codigo` (0 huérfanos).
- Los 24 partidos del AMBA tienen radios (de 187 en Ezeiza a 1.647 en La Matanza).
- Los radios CABA se reparten en las 15 comunas, pero se unen por `censo_codigo` y **no** por `key`
  (`COMUNA 01` vs `COMUNA 1`). Es la primera inconsistencia que el modelo debe absorber.
- `escuelas.localidad` es texto libre con abreviaturas (`L DEL MIRADOR`), así que no sirve
  como clave de localidad. La localidad se asigna espacialmente.

### 2.2 OpenStreetMap (`OSM-DATA-UPDATER-*.bat`)

El script descarga `argentina-latest.osm.pbf` de Geofabrik, recorta el bbox
`-58.99,-34.93,-58.01,-34.33` con `osmium` e importa con `osm2pgsql -c` usando la salida
clásica (`pgsql`) y el estilo por defecto. Eso produce, en el esquema `public`:

| Tabla | Contenido | Geometría |
|---|---|---|
| `planet_osm_point` | Nodos con etiquetas (POIs puntuales, paradas) | `Point, 3857` |
| `planet_osm_line` | Vías, recorridos | `LineString, 3857` |
| `planet_osm_polygon` | Edificios, parques, límites administrativos, POIs con superficie | `Polygon/MultiPolygon, 3857` |
| `planet_osm_roads` | Subconjunto de vías para render | `LineString, 3857` |

Particularidades que afectan al modelo:

1. **SRID 3857** (Web Mercator), mientras que la base compartida usa **4326**. Sin
   normalización, cualquier `ST_Contains` entre ambas falla o da resultados vacíos.
2. **Etiquetas como columnas**: solo existen las claves del estilo por defecto (`amenity`,
   `shop`, `leisure`, `tourism`, `railway`, `public_transport`, `highway`, `name`,
   `operator`, `sport`, `addr:housenumber`, `admin_level`, `boundary`, `place`, …). Claves
   como `healthcare` o `addr:street` **se pierden**. Se agregó `--hstore` al script del
   backend para conservar todas las etiquetas en la columna `tags`.
3. **Un mismo tipo de POI puede venir como punto o como polígono** (una escuela mapeada
   como edificio). Hay que unificarlo en un punto representativo.
4. Los `osm_id` de polígonos que vienen de relaciones son **negativos**; el identificador
   externo estable es `n<id>`, `w<id>` o `r<id>`.
5. Límites administrativos: `boundary = 'administrative'` con `admin_level`. Los niveles
   que se usan para barrios de CABA y localidades del GBA se definen por configuración
   (ver `appsettings.json` → `Ingestion`) y se validan con la primera importación real.

### 2.3 Fuentes oficiales pendientes (planilla de fuentes)

| Capa | Fuente oficial | Estado |
|---|---|---|
| Barrios CABA | Buenos Aires Data, *Barrios* (GeoJSON, 4326) | No está en la base compartida. Se carga desde OSM o desde el GeoJSON oficial con el lector genérico. |
| Comunas CABA | Buenos Aires Data | En `zm.comunas`. |
| Partidos PBA | Datos Abiertos PBA | En `zm.partidos`. |
| Radios censales | INDEC, Censo 2022 | En `zm.radios`. |
| Localidades GBA | OSM (Georef no las distingue) | Se cargan desde OSM. |

## 3. Dos vistas del mismo punto de interés

| Aspecto | Padrón educativo (`zm.escuelas`) | OSM (`planet_osm_*`) |
|---|---|---|
| Identificador | `clave_natural` | `osm_id` + tipo de elemento |
| Categoría | `nivel` (`Nivel Inicial`, `Nivel Primario`, …) + `modalidad` | `amenity=kindergarten/school/college/university` |
| Gestión | `sector` (`Estatal`/`Privado`) | `operator:type` (si se carga `hstore`), en general ausente |
| Geometría | `Point, 4326` | Punto o polígono, `3857` |
| Dirección | `direccion` armada | `addr:*` (parcial) |
| Cobertura | Solo PBA, muy completa | AMBA completo, desparejo |
| Fecha | `periodo` (`Inicial 2026`) | Fecha de descarga del extracto |

**Se confirma el requerimiento**: son dos representaciones distintas del mismo concepto
("un establecimiento educativo de nivel primario en tal lugar"). Si la app consumiera
cualquiera de las dos tablas de forma directa, cada análisis tendría que conocer el
esquema de cada fuente, y un cambio en una fuente (nueva columna, otra proyección) se
propagaría por toda la aplicación.

## 4. Decisiones

1. **Modelo canónico propio**, independiente de las fuentes. Las tablas de origen
   (`zm.*`, `planet_osm_*`) quedan como *staging* de solo lectura.
2. **Una única proyección interna: EPSG:4326**. Todo se transforma al ingresar.
3. **Unidades territoriales en una sola tabla jerárquica** (`territorial_units`), con tipo
   (jurisdicción, comuna, partido, barrio, localidad, radio censal) y padre. La zona de
   análisis (`zones`) referencia a un barrio o localidad.
4. **Puntos de interés en una sola tabla** (`points_of_interest`) con una **taxonomía
   propia** de dos niveles (categoría estándar de las pantallas → subcategoría). Las reglas
   que traducen atributos de cada fuente a esa taxonomía son **configuración en base**
   (`poi_mapping_rules`): sumar una categoría no requiere código (PBI 12).
5. **Trazabilidad obligatoria**: cada unidad, POI e indicador guarda fuente, identificador
   externo y fecha de la fuente (PBI 37a).
6. **Deduplicación entre fuentes**: un POI de OSM y uno del padrón que representan el
   mismo lugar se vinculan con `canonical_poi_id`. La app muestra un único lugar y
   conserva ambas procedencias.
7. Los atributos específicos de cada fuente que no forman parte del modelo (por ejemplo,
   `matricula`) se guardan en `attributes` (`jsonb`), sin ampliar el esquema.
