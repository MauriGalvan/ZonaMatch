-- Pasa los datos de carga a sus tablas definitivas y calcula los indicadores de cada zona (schema geo).
-- Lo corre DATOS-ZONAS-DOCKER.bat despues de copiar los CSV (etl/crear_tablas.sql).
--
-- Las zonas son los limites administrativos de OpenStreetMap (schema osm), igual que en GET /Zonas/{slug}.
-- Cada radio censal y cada delito se asigna por ubicacion a todas las zonas que lo contienen: un barrio o
-- una localidad suma los suyos, y una comuna o un partido suma los de toda su superficie, que son los de
-- los barrios o localidades que lo componen.

\set ON_ERROR_STOP on
SET client_min_messages = warning;

BEGIN;

-- ---------------------------------------------------------------------------------------------------
-- Censo 2022 por radio
-- ---------------------------------------------------------------------------------------------------
DROP TABLE IF EXISTS geo.censo_indicadores, geo.censo_radios CASCADE;

CREATE TABLE geo.censo_radios (
    cod        text PRIMARY KEY,
    prov       text NOT NULL,
    depto      text NOT NULL,
    fracc      text NOT NULL,
    radio      text NOT NULL,
    poblacion  integer NOT NULL,
    viviendas  integer NOT NULL,
    geom       geometry(MultiPolygon, 4326) NOT NULL,
    -- Punto que siempre cae dentro del radio: decide a que zona pertenece
    punto      geometry(Point, 4326) NOT NULL
);

INSERT INTO geo.censo_radios
SELECT cod, prov, depto, fracc, radio, coalesce(poblacion, 0), coalesce(viviendas, 0), g,
       ST_PointOnSurface(g)
FROM (
    SELECT c.*, ST_Multi(ST_CollectionExtract(ST_MakeValid(ST_GeomFromText(c.wkt, 4326)), 3)) AS g
    FROM geo.carga_censo_radios c
) r
WHERE NOT ST_IsEmpty(g);

CREATE INDEX ON geo.censo_radios USING gist (punto);

-- Formato largo: hogares o personas de cada categoria de cada variable, por radio
CREATE TABLE geo.censo_indicadores (
    cod        text NOT NULL REFERENCES geo.censo_radios (cod),
    variable   text NOT NULL,
    categoria  text NOT NULL,
    conteo     integer NOT NULL,
    PRIMARY KEY (cod, variable, categoria)
);

INSERT INTO geo.censo_indicadores
SELECT i.cod, i.variable, i.categoria, sum(i.conteo)
FROM geo.carga_censo_indicadores i
JOIN geo.censo_radios r USING (cod)
GROUP BY 1, 2, 3;

-- ---------------------------------------------------------------------------------------------------
-- Delitos de CABA (Mapa del Delito)
-- ---------------------------------------------------------------------------------------------------
DROP TABLE IF EXISTS geo.delitos_caba;

CREATE TABLE geo.delitos_caba (
    id_mapa    bigint PRIMARY KEY,
    fecha      date NOT NULL,
    franja     integer,
    tipo       text NOT NULL,
    subtipo    text,
    uso_arma   boolean,
    uso_moto   boolean,
    barrio     text,
    comuna     integer,
    cantidad   integer NOT NULL,
    geom       geometry(Point, 4326)
);

INSERT INTO geo.delitos_caba
SELECT id_mapa, fecha, franja, tipo, subtipo, uso_arma = 'SI', uso_moto = 'SI', barrio, comuna,
       coalesce(cantidad, 1),
       CASE WHEN latitud IS NOT NULL AND longitud IS NOT NULL
            THEN ST_SetSRID(ST_MakePoint(longitud, latitud), 4326) END
FROM geo.carga_delitos
ON CONFLICT (id_mapa) DO NOTHING;

CREATE INDEX ON geo.delitos_caba USING gist (geom);

-- ---------------------------------------------------------------------------------------------------
-- Zonas de OpenStreetMap
-- ---------------------------------------------------------------------------------------------------
DROP TABLE IF EXISTS geo.zona_indicadores, geo.zonas;

-- Misma regla que ZonaSql.Slug y SlugZona.Generar: minusculas, sin acentos, lo demas pasa a "-"
CREATE OR REPLACE FUNCTION geo.slug_zona(nombre text) RETURNS text
LANGUAGE sql IMMUTABLE AS $$
    SELECT trim(both '-' from regexp_replace(
        translate(lower(nombre), 'áàäâãéèëêíìïîóòöôõúùüûñç', 'aaaaaeeeeiiiiooooouuuunc'),
        '[^a-z0-9]+', '-', 'g'))
$$;

-- Niveles 4 (CABA) a 10 (barrios). osm2pgsql guarda cada parte de un multipoligono como una fila con el
-- mismo osm_id: se vuelven a unir.
CREATE TABLE geo.zonas (
    osm_id          bigint PRIMARY KEY,
    nombre          text NOT NULL,
    slug            text NOT NULL,
    nivel           integer NOT NULL,
    superficie_km2  double precision NOT NULL,
    geom            geometry(MultiPolygon, 4326) NOT NULL,
    -- La que devuelve GET /Zonas/{slug} cuando hay nombres repetidos: el nivel mas especifico y, si
    -- empatan, la mas grande (misma regla que ZonaSql.ZonaPorSlug)
    elegida         boolean NOT NULL DEFAULT false
);

INSERT INTO geo.zonas (osm_id, nombre, slug, nivel, superficie_km2, geom)
SELECT osm_id, name, geo.slug_zona(name), nivel,
       ST_Area(ST_Transform(g, 4326)::geography) / 1e6,
       ST_Multi(ST_CollectionExtract(ST_MakeValid(ST_Transform(g, 4326)), 3))
FROM (
    SELECT t.osm_id, t.name, t.admin_level::int AS nivel, ST_Union(t.way) AS g, sum(t.way_area) AS area
    FROM osm.planet_osm_polygon t
    WHERE t.boundary = 'administrative' AND t.name IS NOT NULL
      AND t.admin_level ~ '^[0-9]+$' AND t.admin_level::int BETWEEN 4 AND 10
    GROUP BY t.osm_id, t.name, t.admin_level
) z;

UPDATE geo.zonas z SET elegida = true
FROM (
    SELECT DISTINCT ON (slug) osm_id
    FROM geo.zonas
    WHERE nivel BETWEEN 5 AND 10
    ORDER BY slug, nivel DESC, superficie_km2 DESC
) e
WHERE z.osm_id = e.osm_id;

CREATE INDEX ON geo.zonas USING gist (geom);
CREATE INDEX ON geo.zonas (slug) WHERE elegida;

-- ---------------------------------------------------------------------------------------------------
-- Indicadores por zona: conteos crudos. Los porcentajes y las tasas se calculan en la aplicacion
-- (IndicadoresZona), asi la regla de cada indicador vive en el dominio y no en el SQL.
-- ---------------------------------------------------------------------------------------------------
CREATE TABLE geo.zona_indicadores (
    osm_id                       bigint PRIMARY KEY REFERENCES geo.zonas (osm_id),
    radios                       integer NOT NULL,
    poblacion                    integer NOT NULL,
    viviendas                    integer NOT NULL,
    personas_0_14                integer NOT NULL,
    personas_15_64               integer NOT NULL,
    personas_65_mas              integer NOT NULL,
    personas_con_cobertura_salud integer NOT NULL,
    personas_con_dato_cobertura  integer NOT NULL,
    personas_universitario       integer NOT NULL,
    personas_con_dato_educacion  integer NOT NULL,
    hogares                      integer NOT NULL,
    hogares_internet             integer NOT NULL,
    hogares_celular_internet     integer NOT NULL,
    hogares_computadora          integer NOT NULL,
    hogares_nbi                  integer NOT NULL,
    hogares_hacinamiento         integer NOT NULL,
    hogares_alquilan             integer NOT NULL,
    -- Seguridad: solo hay delitos con ubicacion en CABA (null en el resto)
    delitos_anio                 integer,
    delitos_robos                integer,
    delitos_hurtos               integer,
    delitos_lesiones             integer,
    delitos_homicidios           integer,
    delitos_amenazas             integer,
    siniestros_viales            integer
);

WITH asignacion AS (
    SELECT z.osm_id, r.cod, r.poblacion, r.viviendas
    FROM geo.zonas z
    JOIN geo.censo_radios r ON ST_Contains(z.geom, r.punto)
),
censo AS (
    SELECT a.osm_id,
           count(DISTINCT a.cod)::int AS radios,
           sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_EDADGRU' AND i.categoria = '1') AS p_0_14,
           sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_EDADGRU' AND i.categoria = '2') AS p_15_64,
           sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_EDADGRU' AND i.categoria = '3') AS p_65,
           -- Obra social, prepaga o PAMI (1) o plan estatal (2)
           sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_P19' AND i.categoria IN ('1', '2')) AS p_cobertura,
           sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_P19') AS p_dato_cobertura,
           -- Universitario completo o posgrado (9 a 11); "Ignorado" (99) queda afuera del total
           sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_MNI' AND i.categoria IN ('9', '10', '11')) AS p_universitario,
           sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_MNI' AND i.categoria <> '99') AS p_dato_educacion,
           sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_H24A') AS hogares,
           sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_H24A' AND i.categoria = '1') AS h_internet,
           sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_H24B' AND i.categoria = '1') AS h_celular,
           sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_H24C' AND i.categoria = '1') AS h_computadora,
           sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_NBI_TOT' AND i.categoria = '1') AS h_nbi,
           -- Mas de 2 personas por cuarto (INDEC: 2,00 a 3,00 y mas de 3,00)
           sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_H20CP' AND i.categoria IN ('5', '6')) AS h_hacinamiento,
           sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_H22' AND i.categoria = '2') AS h_alquilan
    FROM asignacion a
    JOIN geo.censo_indicadores i USING (cod)
    GROUP BY a.osm_id
),
totales AS (
    SELECT osm_id, sum(poblacion)::int AS poblacion, sum(viviendas)::int AS viviendas
    FROM asignacion
    GROUP BY osm_id
),
-- Zonas dentro de CABA (nivel 4): son las que tienen delitos con ubicacion
zonas_caba AS (
    SELECT z.osm_id, z.geom
    FROM geo.zonas z
    JOIN geo.zonas caba ON caba.nivel = 4 AND ST_Contains(caba.geom, ST_PointOnSurface(z.geom))
),
delitos AS (
    SELECT z.osm_id,
           coalesce(sum(d.cantidad) FILTER (WHERE d.tipo <> 'Vialidad'), 0)::int AS total,
           coalesce(sum(d.cantidad) FILTER (WHERE d.tipo = 'Robo'), 0)::int AS robos,
           coalesce(sum(d.cantidad) FILTER (WHERE d.tipo = 'Hurto'), 0)::int AS hurtos,
           coalesce(sum(d.cantidad) FILTER (WHERE d.tipo = 'Lesiones'), 0)::int AS lesiones,
           coalesce(sum(d.cantidad) FILTER (WHERE d.tipo = 'Homicidios'), 0)::int AS homicidios,
           coalesce(sum(d.cantidad) FILTER (WHERE d.tipo = 'Amenazas'), 0)::int AS amenazas,
           coalesce(sum(d.cantidad) FILTER (WHERE d.tipo = 'Vialidad'), 0)::int AS viales
    FROM zonas_caba z
    LEFT JOIN geo.delitos_caba d ON ST_Contains(z.geom, d.geom)
    GROUP BY z.osm_id
)
INSERT INTO geo.zona_indicadores
SELECT t.osm_id, c.radios, t.poblacion, t.viviendas,
       coalesce(c.p_0_14, 0), coalesce(c.p_15_64, 0), coalesce(c.p_65, 0),
       coalesce(c.p_cobertura, 0), coalesce(c.p_dato_cobertura, 0),
       coalesce(c.p_universitario, 0), coalesce(c.p_dato_educacion, 0),
       coalesce(c.hogares, 0), coalesce(c.h_internet, 0), coalesce(c.h_celular, 0),
       coalesce(c.h_computadora, 0), coalesce(c.h_nbi, 0), coalesce(c.h_hacinamiento, 0),
       coalesce(c.h_alquilan, 0),
       -- Dentro de CABA una zona sin delitos registrados tiene 0; fuera de CABA queda null (sin datos)
       d.total, d.robos, d.hurtos, d.lesiones, d.homicidios, d.amenazas, d.viales
FROM totales t
JOIN censo c USING (osm_id)
LEFT JOIN delitos d USING (osm_id);

DROP TABLE geo.carga_censo_radios, geo.carga_censo_indicadores, geo.carga_delitos;

COMMIT;

SELECT nivel, count(*) AS zonas, count(i.osm_id) AS con_indicadores, count(i.delitos_anio) AS con_delitos
FROM geo.zonas z LEFT JOIN geo.zona_indicadores i USING (osm_id)
GROUP BY nivel ORDER BY nivel;
