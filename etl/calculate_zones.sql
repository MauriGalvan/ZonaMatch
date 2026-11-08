-- Moves staged data into its final tables and calculates each zone's indicators (geo schema).
-- ZONE-DATA-DOCKER.bat runs this after copying the CSV files (etl/create_geographic_tables.sql).
--
-- Zones are OpenStreetMap administrative boundaries (osm schema), as in GET /api/v1/zones/{slug}.
-- Safety data: geolocated incidents from the CABA Crime Map; in Buenos Aires province, SNIC crimes are distributed
-- across localities in each district by population (SNIC only reports district-level data).
-- Each census tract and crime is assigned to every zone containing its location: a neighborhood or locality gets
-- its own records, while a commune or district gets all records within its boundaries.

\set ON_ERROR_STOP on
SET client_min_messages = warning;

BEGIN;

-- ---------------------------------------------------------------------------------------------------
-- 2022 census by tract.
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
    -- A point guaranteed to fall inside the tract, used to determine which zone contains it.
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

-- Long format: household or person counts for each category and variable, by tract.
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
-- CABA crime data (Crime Map).
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
-- OpenStreetMap zones.
-- ---------------------------------------------------------------------------------------------------
-- Transit tables (etl/calculate_transit.sql) also depend on geo.zonas.
DROP TABLE IF EXISTS geo.zona_transporte_lineas, geo.zona_transporte_paradas, geo.zona_indicadores, geo.zonas;

-- Same rule as ZoneSql.Slug and SlugZona.Generate: lowercase, remove accents, and replace other characters with "-".
CREATE OR REPLACE FUNCTION geo.slug_zona(nombre text) RETURNS text
LANGUAGE sql IMMUTABLE AS $$
    SELECT trim(both '-' from regexp_replace(
        translate(lower(nombre), 'áàäâãéèëêíìïîóòöôõúùüûñç', 'aaaaaeeeeiiiiooooouuuunc'),
        '[^a-z0-9]+', '-', 'g'))
$$;

-- Levels 4 (CABA) through 10 (neighborhoods). osm2pgsql stores each part of a multipolygon as a row with the
-- same osm_id, so the parts are merged again.
CREATE TABLE geo.zonas (
    osm_id          bigint PRIMARY KEY,
    nombre          text NOT NULL,
    slug            text NOT NULL,
    nivel           integer NOT NULL,
    superficie_km2  double precision NOT NULL,
    geom            geometry(MultiPolygon, 4326) NOT NULL,
    -- For duplicate names, choose the result returned by GET /api/v1/zones/{slug}: the most specific level and, if
    -- tied, the largest zone (same rule as ZoneSql.ZoneBySlug).
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

-- Different places with the same name (Villa Sarmiento in Morón and Lanús) would get the same slug, leaving only
-- one visible. The others get a slug with their district or commune (villa-sarmiento-moron), as Zonaprop does;
-- the selected zone keeps its existing slug so links and saved analyses do not change. If two levels refer to the
-- same area (one contains the other), the less specific one remains hidden.
WITH duplicate_zones AS (
    SELECT n.osm_id, n.slug, n.geom
    FROM geo.zonas n
    JOIN geo.zonas e ON e.slug = n.slug AND e.elegida
    WHERE NOT n.elegida AND n.nivel BETWEEN 5 AND 10
      AND NOT ST_Contains(e.geom, ST_PointOnSurface(n.geom))
      AND NOT ST_Contains(n.geom, ST_PointOnSurface(e.geom))
),
renamed_zones AS (
    SELECT r.osm_id,
           r.slug || '-' || coalesce(
               (SELECT regexp_replace(p.slug, '^partido-de-', '') FROM geo.zonas p
                WHERE p.nivel = 5 AND ST_Contains(p.geom, ST_PointOnSurface(r.geom))
                ORDER BY p.superficie_km2 LIMIT 1),
               (SELECT l.slug FROM geo.zonas l
                WHERE l.nivel = 8 AND l.osm_id <> r.osm_id AND ST_Contains(l.geom, ST_PointOnSurface(r.geom))
                ORDER BY l.superficie_km2 LIMIT 1),
               abs(r.osm_id)::text) AS slug
    FROM duplicate_zones r
),
unique_zone_slugs AS (
    -- If two slugs still match (same name and district) or collide with another zone, append the OSM ID.
    SELECT n.osm_id,
           CASE WHEN count(*) OVER (PARTITION BY n.slug) > 1
                     OR EXISTS (SELECT 1 FROM geo.zonas o WHERE o.slug = n.slug)
                THEN n.slug || '-' || abs(n.osm_id) ELSE n.slug END AS slug
    FROM renamed_zones n
)
UPDATE geo.zonas z SET slug = u.slug, elegida = true
FROM unique_zone_slugs u
WHERE z.osm_id = u.osm_id;

CREATE INDEX ON geo.zonas USING gist (geom);
CREATE UNIQUE INDEX ON geo.zonas (slug) WHERE elegida;

-- ---------------------------------------------------------------------------------------------------
-- Raw indicator counts by zone. Percentages and rates are calculated by the application so each indicator's rule
-- remains in the domain layer instead of SQL.
-- ---------------------------------------------------------------------------------------------------
CREATE TABLE geo.zona_indicadores (
    osm_id                       bigint PRIMARY KEY REFERENCES geo.zonas (osm_id),
    radios                       integer NOT NULL,
    poblacion                    integer NOT NULL,
    viviendas                    integer NOT NULL,
    personas_0_4                 integer NOT NULL,
    personas_5_14                integer NOT NULL,
    personas_15_29               integer NOT NULL,
    personas_30_44               integer NOT NULL,
    personas_45_64               integer NOT NULL,
    personas_65_mas              integer NOT NULL,
    edad_promedio                double precision,
    personas_0_14                integer NOT NULL,
    personas_15_64               integer NOT NULL,
    personas_con_cobertura_salud integer NOT NULL,
    personas_con_dato_cobertura  integer NOT NULL,
    personas_universitario       integer NOT NULL,
    personas_con_dato_educacion  integer NOT NULL,
    hogares                      integer NOT NULL,
    hogares_unipersonales        integer NOT NULL,
    hogares_internet             integer NOT NULL,
    hogares_celular_internet     integer NOT NULL,
    hogares_computadora          integer NOT NULL,
    hogares_nbi                  integer NOT NULL,
    hogares_hacinamiento         integer NOT NULL,
    hogares_alquilan             integer NOT NULL,
    hogares_agua_red             integer NOT NULL DEFAULT 0,
    hogares_agua_red_pct         double precision,
    hogares_cloacas              integer NOT NULL DEFAULT 0,
    hogares_cloacas_pct          double precision,
    hogares_gas_red              integer NOT NULL DEFAULT 0,
    hogares_gas_red_pct          double precision,
    hogares_calidad_buena        integer NOT NULL DEFAULT 0,
    hogares_calidad_buena_pct    double precision,
    personas_secundario_completo integer NOT NULL DEFAULT 0,
    personas_secundario_pct      double precision,
    -- Safety data for delitos_periodo. CABA: Crime Map (delitos_fuente 'mapa-delito'); province: population-weighted
    -- SNIC district totals ('snic'), with source districts recorded in delitos_ambito.
    delitos_anio                 integer,
    delitos_robos                integer,
    delitos_hurtos               integer,
    delitos_lesiones             integer,
    delitos_homicidios           integer,
    delitos_amenazas             integer,
    siniestros_viales            integer,
    delitos_fuente               text,
    delitos_periodo              integer,
    delitos_ambito               text,
    -- Crimes scaled to SNIC's CABA rates so CABA and province can be compared; scoring uses these values. In CABA,
    -- Crime Map values are scaled by the ratio of citywide SNIC to Crime Map totals (preserving neighborhood
    -- differences). In the province, SNIC values are adjusted using the victimization survey. Both are combined
    -- with five-year homicide rates by district or commune to correct for differences in reporting.
    delitos_comparables          integer,
    -- Province: factor used by etl/crime/spread_crimes.sql to distribute district crimes across its localities
    -- (1 = the district rate); null in CABA and until that step has run.
    delitos_reparto              double precision,
    -- Factor used by that step to distribute district or commune homicides based on zone NBI.
    delitos_reparto_homicidios   double precision
);

WITH census_assignments AS (
    SELECT z.osm_id, r.cod, r.poblacion, r.viviendas
    FROM geo.zonas z
    JOIN geo.censo_radios r ON ST_Contains(z.geom, r.punto)
),
census_counts AS (
    SELECT a.osm_id,
           count(DISTINCT a.cod)::int AS radios,
           coalesce(sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_EDADQUI' AND i.categoria = '1'), 0)::int AS p_0_4,
           coalesce(sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_EDADQUI' AND i.categoria IN ('2', '3')), 0)::int AS p_5_14,
           coalesce(sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_EDADQUI' AND i.categoria IN ('4', '5', '6')), 0)::int AS p_15_29,
           coalesce(sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_EDADQUI' AND i.categoria IN ('7', '8', '9')), 0)::int AS p_30_44,
           coalesce(sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_EDADQUI' AND i.categoria IN ('10', '11', '12', '13')), 0)::int AS p_45_64,
           coalesce(sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_EDADQUI' AND i.categoria ~ '^[0-9]+$' AND i.categoria::int >= 14), 0)::int AS p_65,
           round(
               sum(i.categoria::int * i.conteo) FILTER (WHERE i.variable = 'PERSONA_EDAD' AND i.categoria ~ '^[0-9]+$')::numeric
               / nullif(sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_EDAD' AND i.categoria ~ '^[0-9]+$'), 0)
           , 1)::double precision AS edad_promedio,
           coalesce(sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_EDADQUI' AND i.categoria IN ('1', '2', '3')), 0)::int AS p_0_14,
           coalesce(sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_EDADQUI' AND i.categoria ~ '^[0-9]+$' AND i.categoria::int BETWEEN 4 AND 13), 0)::int AS p_15_64,
           -- Employer health plan, private insurance, or PAMI (1), or state health plan (2).
           sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_P19' AND i.categoria IN ('1', '2')) AS p_cobertura,
           sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_P19') AS p_dato_cobertura,
           -- Completed university or postgraduate education (9 to 11); "Unknown" (99) is excluded from the total.
           sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_MNI' AND i.categoria IN ('9', '10', '11')) AS p_universitario,
           sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_MNI' AND i.categoria <> '99') AS p_dato_educacion,
           sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_H24A') AS hogares,
           coalesce(sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_TIPHOGAR' AND i.categoria = '1'), 0)::int AS h_unipersonales,
           sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_H24A' AND i.categoria = '1') AS h_internet,
           sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_H24B' AND i.categoria = '1') AS h_celular,
           sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_H24C' AND i.categoria = '1') AS h_computadora,
           sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_NBI_TOT' AND i.categoria = '1') AS h_nbi,
           -- More than 2 people per room (INDEC: 2.00 to 3.00 and above 3.00).
           sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_H20CP' AND i.categoria IN ('5', '6')) AS h_hacinamiento,
           sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_H22' AND i.categoria = '2') AS h_alquilan,
           coalesce(sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_H14' AND i.categoria = '1'), 0)::int AS h_agua_red,
           coalesce(sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_H18' AND i.categoria = '1'), 0)::int AS h_cloacas,
           coalesce(sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_H19' AND i.categoria = '2'), 0)::int AS h_gas_red,
           coalesce(sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_INMAT' AND i.categoria = '1'), 0)::int AS h_calidad_buena,
           coalesce(sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_MNI' AND i.categoria ~ '^[0-9]+$' AND i.categoria::int BETWEEN 5 AND 11), 0)::int AS p_secundario
    FROM census_assignments a
    JOIN geo.censo_indicadores i USING (cod)
    GROUP BY a.osm_id
),
zone_totals AS (
    SELECT osm_id, sum(poblacion)::int AS poblacion, sum(viviendas)::int AS viviendas
    FROM census_assignments
    GROUP BY osm_id
),
-- Zones within CABA (level 4): these have geolocated crime data.
caba_zones AS (
    SELECT z.osm_id, z.geom
    FROM geo.zonas z
    JOIN geo.zonas caba ON caba.nivel = 4 AND ST_Contains(caba.geom, ST_PointOnSurface(z.geom))
),
crime_map_totals AS (
    SELECT z.osm_id,
           coalesce(sum(d.cantidad) FILTER (WHERE d.tipo <> 'Vialidad'), 0)::int AS total,
           coalesce(sum(d.cantidad) FILTER (WHERE d.tipo = 'Robo'), 0)::int AS robos,
           coalesce(sum(d.cantidad) FILTER (WHERE d.tipo = 'Hurto'), 0)::int AS hurtos,
           coalesce(sum(d.cantidad) FILTER (WHERE d.tipo = 'Lesiones'), 0)::int AS lesiones,
           coalesce(sum(d.cantidad) FILTER (WHERE d.tipo = 'Homicidios'), 0)::int AS homicidios,
           coalesce(sum(d.cantidad) FILTER (WHERE d.tipo = 'Amenazas'), 0)::int AS amenazas,
           coalesce(sum(d.cantidad) FILTER (WHERE d.tipo = 'Vialidad'), 0)::int AS viales
    FROM caba_zones z
    LEFT JOIN geo.delitos_caba d ON ST_Contains(z.geom, d.geom)
    GROUP BY z.osm_id
),
-- SNIC rate by tract: its district (or commune) rate per population. Census and SNIC department codes match in the
-- province; in CABA, census commune numbers advance by 7 (007 is Commune 1, 014 is Commune 2, and so on).
snic_tract_rates AS (
    SELECT r.cod, r.poblacion, r.prov = '02' AS en_caba, s.department_name, s.year, s.crime_group,
           r.poblacion * s.rate_per_100k / 100000.0 AS hechos
    FROM geo.censo_radios r
    JOIN geo.carga_snic s ON s.department_id = r.prov || CASE WHEN r.prov = '02'
                                                             THEN lpad((r.depto::int / 7)::text, 3, '0')
                                                             ELSE r.depto END
),
-- Recorded crimes are not directly comparable between CABA and the province: fewer crimes are reported in the
-- province, while CABA crimes affecting commuters are divided only by the resident population. Recorded data show
-- 2.7 times more robberies per resident in CABA; the National Victimization Survey, which measures crimes whether
-- reported or not, shows 32% more in the province. Each provincial crime group is multiplied by a factor that
-- adjusts the recorded province/CABA ratio to the survey ratio.
-- Source: INDEC, 2017 National Victimization Survey, Table 1 (prevalence among people aged 18 and older,
-- 2016; "Buenos Aires" refers to the entire province). Injuries have no separate prevalence estimate and use the
-- violent robbery rate. Homicides are almost always recorded and are not adjusted.
victimization_rates (crime_group, caba, provincia) AS (
    VALUES ('thefts', 11.9, 7.4), ('robberies', 5.9, 7.8), ('injuries', 5.9, 7.8), ('threats', 3.6, 4.6)
),
recorded_rates AS (
    SELECT crime_group,
           sum(hechos) FILTER (WHERE en_caba) / nullif(sum(poblacion) FILTER (WHERE en_caba), 0) AS caba,
           sum(hechos) FILTER (WHERE NOT en_caba) / nullif(sum(poblacion) FILTER (WHERE NOT en_caba), 0) AS provincia
    FROM snic_tract_rates
    GROUP BY crime_group
),
reporting_adjustments AS (
    SELECT r.crime_group, (e.provincia / e.caba) / nullif(r.provincia / nullif(r.caba, 0), 0) AS factor
    FROM recorded_rates r
    JOIN victimization_rates e USING (crime_group)
),
snic_zone_totals AS (
    SELECT a.osm_id,
           max(s.year) AS periodo,
           round(sum(s.hechos * CASE WHEN s.en_caba THEN 1 ELSE coalesce(c.factor, 1) END)
                 FILTER (WHERE s.crime_group NOT IN ('road', 'homicides_5y')))::int AS comparables,
           round(sum(s.hechos) FILTER (WHERE s.crime_group NOT IN ('road', 'homicides_5y')))::int AS total,
           sum(s.hechos) FILTER (WHERE s.crime_group = 'homicides_5y') AS homicidios_5a,
           round(sum(s.hechos) FILTER (WHERE s.crime_group = 'robberies'))::int AS robos,
           round(sum(s.hechos) FILTER (WHERE s.crime_group = 'thefts'))::int AS hurtos,
           round(sum(s.hechos) FILTER (WHERE s.crime_group = 'injuries'))::int AS lesiones,
           round(sum(s.hechos) FILTER (WHERE s.crime_group = 'homicides'))::int AS homicidios,
           round(sum(s.hechos) FILTER (WHERE s.crime_group = 'threats'))::int AS amenazas,
           round(sum(s.hechos) FILTER (WHERE s.crime_group = 'road'))::int AS viales
    FROM census_assignments a
    JOIN snic_tract_rates s USING (cod)
    LEFT JOIN reporting_adjustments c USING (crime_group)
    GROUP BY a.osm_id
),
-- Districts contributing population to each zone, ordered from largest to smallest (usually one district).
zone_source_areas AS (
    SELECT osm_id, string_agg(department_name, ', ' ORDER BY poblacion DESC) AS ambito
    FROM (
        SELECT a.osm_id, s.department_name, sum(a.poblacion) AS poblacion
        FROM census_assignments a
        JOIN (SELECT DISTINCT cod, department_name FROM snic_tract_rates) s USING (cod)
        GROUP BY a.osm_id, s.department_name
    ) p
    GROUP BY osm_id
),
-- Ratio used to scale the CABA Crime Map to SNIC figures.
caba_crime_scale AS (
    SELECT s.total::numeric / nullif(m.total, 0) AS factor
    FROM geo.zonas caba
    JOIN crime_map_totals m ON m.osm_id = caba.osm_id
    JOIN snic_zone_totals s ON s.osm_id = caba.osm_id
    WHERE caba.nivel = 4
    ORDER BY caba.superficie_km2 DESC
    LIMIT 1
),
base_crime_totals AS (
    SELECT m.osm_id, m.total, m.robos, m.hurtos, m.lesiones, m.homicidios, m.amenazas, m.viales,
           'mapa-delito' AS fuente, (SELECT max(anio) FROM geo.carga_delitos) AS periodo, NULL::text AS ambito,
           m.total * (SELECT factor FROM caba_crime_scale) AS estimados, s.homicidios_5a
    FROM crime_map_totals m
    LEFT JOIN snic_zone_totals s USING (osm_id)
    UNION ALL
    SELECT s.osm_id, s.total, s.robos, s.hurtos, s.lesiones, s.homicidios, s.amenazas, s.viales,
           'snic', s.periodo, b.ambito, s.comparables, s.homicidios_5a
    FROM snic_zone_totals s
    LEFT JOIN zone_source_areas b USING (osm_id)
    WHERE s.osm_id NOT IN (SELECT osm_id FROM crime_map_totals)
),
-- Reporting also varies within each region: Morón records more robberies and thefts per resident than Moreno but
-- half as many homicides; the same pattern appears between northern and southern CABA communes. Homicides are
-- almost always recorded, so the final value is the geometric mean of estimated crimes and 2021-2025 homicides
-- (SNIC, by district or commune), scaled to regional crime figures so CABA and province remain aligned with the
-- survey. Differences between neighborhoods in the same commune (Crime Map) are preserved.
homicide_scale AS (
    SELECT d.fuente, sum(d.estimados) / nullif(sum(d.homicidios_5a), 0) AS factor
    FROM base_crime_totals d
    JOIN geo.zonas z USING (osm_id)
    WHERE z.elegida AND z.nivel = 5
    GROUP BY d.fuente
),
crime_totals AS (
    SELECT d.*,
           round(CASE WHEN d.homicidios_5a > 0 AND e.factor IS NOT NULL
                      THEN sqrt(d.estimados * d.homicidios_5a * e.factor)
                      ELSE d.estimados END)::int AS comparables
    FROM base_crime_totals d
    LEFT JOIN homicide_scale e USING (fuente)
)
INSERT INTO geo.zona_indicadores
SELECT t.osm_id, c.radios, t.poblacion, t.viviendas,
       coalesce(c.p_0_4, 0), coalesce(c.p_5_14, 0), coalesce(c.p_15_29, 0),
       coalesce(c.p_30_44, 0), coalesce(c.p_45_64, 0), coalesce(c.p_65, 0),
       c.edad_promedio,
       coalesce(c.p_0_14, 0), coalesce(c.p_15_64, 0),
       coalesce(c.p_cobertura, 0), coalesce(c.p_dato_cobertura, 0),
       coalesce(c.p_universitario, 0), coalesce(c.p_dato_educacion, 0),
       coalesce(c.hogares, 0), coalesce(c.h_unipersonales, 0), coalesce(c.h_internet, 0), coalesce(c.h_celular, 0),
       coalesce(c.h_computadora, 0), coalesce(c.h_nbi, 0), coalesce(c.h_hacinamiento, 0),
       coalesce(c.h_alquilan, 0),
       coalesce(c.h_agua_red, 0),
       round(100.0 * coalesce(c.h_agua_red, 0) / nullif(c.hogares, 0), 1)::double precision,
       coalesce(c.h_cloacas, 0),
       round(100.0 * coalesce(c.h_cloacas, 0) / nullif(c.hogares, 0), 1)::double precision,
       coalesce(c.h_gas_red, 0),
       round(100.0 * coalesce(c.h_gas_red, 0) / nullif(c.hogares, 0), 1)::double precision,
       coalesce(c.h_calidad_buena, 0),
       round(100.0 * coalesce(c.h_calidad_buena, 0) / nullif(c.hogares, 0), 1)::double precision,
       coalesce(c.p_secundario, 0),
       round(100.0 * coalesce(c.p_secundario, 0) / nullif(c.p_dato_educacion, 0), 1)::double precision,
       -- In CABA, a zone with no recorded crimes has 0; without SNIC data, the value is null.
       d.total, d.robos, d.hurtos, d.lesiones, d.homicidios, d.amenazas, d.viales,
       -- Per-resident rates are not meaningful for places with very few residents (clubs, small gated communities);
       -- scoring treats these values as missing.
       d.fuente, d.periodo, d.ambito, CASE WHEN t.poblacion >= 500 THEN d.comparables END, NULL, NULL
FROM zone_totals t
JOIN census_counts c USING (osm_id)
LEFT JOIN crime_totals d USING (osm_id);

DROP TABLE geo.carga_censo_radios, geo.carga_censo_indicadores, geo.carga_delitos, geo.carga_snic;

COMMIT;

SELECT nivel, count(*) AS zones, count(i.osm_id) AS zones_with_indicators, count(i.delitos_anio) AS zones_with_crime_data,
       count(i.delitos_anio) FILTER (WHERE i.delitos_fuente = 'snic') AS zones_with_snic_crime_data
FROM geo.zonas z LEFT JOIN geo.zona_indicadores i USING (osm_id)
GROUP BY nivel ORDER BY nivel;
