-- Pasa los datos de carga a sus tablas definitivas y calcula los indicadores de cada zona (schema geo).
-- Lo corre DATOS-ZONAS-DOCKER.bat despues de copiar los CSV (etl/crear_tablas.sql).
--
-- Las zonas son los limites administrativos de OpenStreetMap (schema osm), igual que en GET /Zonas/{slug}.
-- Seguridad: en CABA, los hechos georreferenciados del Mapa del Delito; en provincia, los delitos del SNIC por
-- partido repartidos entre sus localidades segun su poblacion (solo hay dato por partido).
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
-- Las tablas de transporte (etl/calcular_transporte.sql) tambien dependen de geo.zonas
DROP TABLE IF EXISTS geo.zona_transporte_lineas, geo.zona_transporte_paradas, geo.zona_indicadores, geo.zonas;

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
    -- Seguridad del anio delitos_periodo. CABA: Mapa del Delito (delitos_fuente 'mapa-delito'); provincia:
    -- SNIC del partido repartido por poblacion ('snic'), con los partidos de origen en delitos_ambito
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
    -- Delitos en la escala del SNIC de CABA, comparables entre CABA y provincia: los usa el puntaje. En CABA son
    -- los del Mapa del Delito por el cociente SNIC / Mapa del Delito de toda la ciudad (asi se conserva la
    -- diferencia entre barrios); en provincia, los del SNIC corregidos con la encuesta de victimizacion. En las dos,
    -- combinados con los homicidios de 5 anios del partido o la comuna para corregir cuanto se denuncia en cada uno
    delitos_comparables          integer,
    -- Provincia: factor con el que etl/crime/spread_crimes.sql repartio los delitos del partido entre sus
    -- localidades (1 = la tasa del partido); null en CABA y hasta que corre ese paso
    delitos_reparto              double precision
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
delitos_mapa AS (
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
),
-- SNIC por radio: la tasa de su partido (o comuna) por su poblacion. Los codigos de departamento del censo y del
-- SNIC coinciden en provincia; en CABA el censo numera las comunas de a 7 (007 es la Comuna 1, 014 la 2...)
snic_radio AS (
    SELECT r.cod, r.poblacion, r.prov = '02' AS en_caba, s.department_name, s.year, s.crime_group,
           r.poblacion * s.rate_per_100k / 100000.0 AS hechos
    FROM geo.censo_radios r
    JOIN geo.carga_snic s ON s.department_id = r.prov || CASE WHEN r.prov = '02'
                                                             THEN lpad((r.depto::int / 7)::text, 3, '0')
                                                             ELSE r.depto END
),
-- Los delitos registrados no se comparan bien entre CABA y provincia: en provincia se denuncia menos y en CABA
-- los delitos que sufren quienes entran a trabajar se dividen solo por los residentes. Con los registros, CABA
-- tiene 2,7 veces mas robos por habitante; segun la Encuesta Nacional de Victimizacion, que mide los delitos
-- sufridos se hayan denunciado o no, la provincia tiene un 32 % mas. Cada grupo de provincia se multiplica por el
-- factor que lleva la relacion provincia / CABA de los registros a la de la encuesta.
-- Fuente: INDEC, Encuesta Nacional de Victimizacion 2017, Cuadro 1 (prevalencia en personas de 18 anios y mas,
-- anio 2016; "Buenos Aires" es toda la provincia). Las lesiones no tienen prevalencia propia: usan la del robo con
-- violencia. Los homicidios se registran casi siempre y no se corrigen.
encuesta (crime_group, caba, provincia) AS (
    VALUES ('thefts', 11.9, 7.4), ('robberies', 5.9, 7.8), ('injuries', 5.9, 7.8), ('threats', 3.6, 4.6)
),
registrados AS (
    SELECT crime_group,
           sum(hechos) FILTER (WHERE en_caba) / nullif(sum(poblacion) FILTER (WHERE en_caba), 0) AS caba,
           sum(hechos) FILTER (WHERE NOT en_caba) / nullif(sum(poblacion) FILTER (WHERE NOT en_caba), 0) AS provincia
    FROM snic_radio
    GROUP BY crime_group
),
calibracion AS (
    SELECT r.crime_group, (e.provincia / e.caba) / nullif(r.provincia / nullif(r.caba, 0), 0) AS factor
    FROM registrados r
    JOIN encuesta e USING (crime_group)
),
delitos_snic AS (
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
    FROM asignacion a
    JOIN snic_radio s USING (cod)
    LEFT JOIN calibracion c USING (crime_group)
    GROUP BY a.osm_id
),
-- Partidos de cada zona, del que mas poblacion le aporta al que menos (casi siempre uno solo)
ambitos AS (
    SELECT osm_id, string_agg(department_name, ', ' ORDER BY poblacion DESC) AS ambito
    FROM (
        SELECT a.osm_id, s.department_name, sum(a.poblacion) AS poblacion
        FROM asignacion a
        JOIN (SELECT DISTINCT cod, department_name FROM snic_radio) s USING (cod)
        GROUP BY a.osm_id, s.department_name
    ) p
    GROUP BY osm_id
),
-- Cociente para llevar el Mapa del Delito de CABA a la escala del SNIC
escala_caba AS (
    SELECT s.total::numeric / nullif(m.total, 0) AS factor
    FROM geo.zonas caba
    JOIN delitos_mapa m ON m.osm_id = caba.osm_id
    JOIN delitos_snic s ON s.osm_id = caba.osm_id
    WHERE caba.nivel = 4
    ORDER BY caba.superficie_km2 DESC
    LIMIT 1
),
delitos_base AS (
    SELECT m.osm_id, m.total, m.robos, m.hurtos, m.lesiones, m.homicidios, m.amenazas, m.viales,
           'mapa-delito' AS fuente, (SELECT max(anio) FROM geo.carga_delitos) AS periodo, NULL::text AS ambito,
           m.total * (SELECT factor FROM escala_caba) AS estimados, s.homicidios_5a
    FROM delitos_mapa m
    LEFT JOIN delitos_snic s USING (osm_id)
    UNION ALL
    SELECT s.osm_id, s.total, s.robos, s.hurtos, s.lesiones, s.homicidios, s.amenazas, s.viales,
           'snic', s.periodo, b.ambito, s.comparables, s.homicidios_5a
    FROM delitos_snic s
    LEFT JOIN ambitos b USING (osm_id)
    WHERE s.osm_id NOT IN (SELECT osm_id FROM delitos_mapa)
),
-- Dentro de cada region tambien se denuncia distinto: Moron registra mas robos y hurtos por habitante que Moreno
-- pero tiene la mitad de homicidios, y en CABA pasa lo mismo entre las comunas del norte y del sur. Los
-- homicidios se registran casi siempre: el valor final es la media geometrica de los delitos estimados y de los
-- homicidios de 2021-2025 (SNIC, por partido o comuna), llevados a la escala de los delitos de su region para que el
-- nivel de CABA y de la provincia siga siendo el de la encuesta. Las diferencias entre barrios de una misma comuna
-- (Mapa del Delito) se conservan.
escala_homicidios AS (
    SELECT d.fuente, sum(d.estimados) / nullif(sum(d.homicidios_5a), 0) AS factor
    FROM delitos_base d
    JOIN geo.zonas z USING (osm_id)
    WHERE z.elegida AND z.nivel = 5
    GROUP BY d.fuente
),
delitos AS (
    SELECT d.*,
           round(CASE WHEN d.homicidios_5a > 0 AND e.factor IS NOT NULL
                      THEN sqrt(d.estimados * d.homicidios_5a * e.factor)
                      ELSE d.estimados END)::int AS comparables
    FROM delitos_base d
    LEFT JOIN escala_homicidios e USING (fuente)
)
INSERT INTO geo.zona_indicadores
SELECT t.osm_id, c.radios, t.poblacion, t.viviendas,
       coalesce(c.p_0_14, 0), coalesce(c.p_15_64, 0), coalesce(c.p_65, 0),
       coalesce(c.p_cobertura, 0), coalesce(c.p_dato_cobertura, 0),
       coalesce(c.p_universitario, 0), coalesce(c.p_dato_educacion, 0),
       coalesce(c.hogares, 0), coalesce(c.h_internet, 0), coalesce(c.h_celular, 0),
       coalesce(c.h_computadora, 0), coalesce(c.h_nbi, 0), coalesce(c.h_hacinamiento, 0),
       coalesce(c.h_alquilan, 0),
       -- Dentro de CABA una zona sin delitos registrados tiene 0; sin dato del SNIC queda null
       d.total, d.robos, d.hurtos, d.lesiones, d.homicidios, d.amenazas, d.viales,
       -- Con muy pocos habitantes (clubes, countries chicos) una tasa por habitante no dice nada: el puntaje la
       -- toma como dato faltante
       d.fuente, d.periodo, d.ambito, CASE WHEN t.poblacion >= 500 THEN d.comparables END, NULL
FROM totales t
JOIN censo c USING (osm_id)
LEFT JOIN delitos d USING (osm_id);

DROP TABLE geo.carga_censo_radios, geo.carga_censo_indicadores, geo.carga_delitos, geo.carga_snic;

COMMIT;

SELECT nivel, count(*) AS zonas, count(i.osm_id) AS con_indicadores, count(i.delitos_anio) AS con_delitos,
       count(i.delitos_anio) FILTER (WHERE i.delitos_fuente = 'snic') AS con_delitos_snic
FROM geo.zonas z LEFT JOIN geo.zona_indicadores i USING (osm_id)
GROUP BY nivel ORDER BY nivel;
