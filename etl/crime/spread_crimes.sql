-- Distributes crimes within each district (and homicides within each CABA commune) using census fraction factors
-- from etl/crime/fit_crime_model.py, loaded into geo.carga_delitos_fracciones. DATOS-ZONAS-DOCKER.bat runs this
-- immediately after calcular_zonas.sql, which assigns district crime rates to localities and commune homicides
-- to CABA neighborhoods.
--
-- Two factors per zone, each the mean across its census tracts divided by the population-weighted mean for its
-- district or commune. This preserves totals and only changes how they are distributed:
--   delitos_reparto            activity model (where crimes are reported): province only; CABA already has
--                              geolocated Crime Map data. Recorded crimes are multiplied by this factor.
--   delitos_reparto_homicidios NBI (where violence occurs): province and CABA. Homicides are multiplied by this factor.
-- delitos_comparables is the geometric mean of estimated crimes and homicides, so it uses the square root of each.

\set ON_ERROR_STOP on
BEGIN;

WITH census_tracts AS (
    SELECT r.prov, r.poblacion, r.punto,
           -- District, or commune in CABA (the census numbers communes by 7).
           CASE WHEN r.prov = '02' THEN (r.depto::int / 7)::text ELSE r.depto END AS ambito,
           coalesce(f.factor, 1) AS factor, coalesce(f.homicide_factor, 1) AS homicidios
    FROM geo.censo_radios r
    LEFT JOIN geo.carga_delitos_fracciones f ON f.fraction_id = r.prov || r.depto || r.fracc
),
district_averages AS (
    SELECT prov, ambito,
           sum(poblacion * factor) / nullif(sum(poblacion), 0) AS factor_medio,
           sum(poblacion * homicidios) / nullif(sum(poblacion), 0) AS homicidios_medio
    FROM census_tracts
    GROUP BY prov, ambito
),
zone_factors AS (
    SELECT z.osm_id,
           sum(r.poblacion * r.factor / a.factor_medio) / nullif(sum(r.poblacion), 0) AS factor,
           sum(r.poblacion * r.homicidios / a.homicidios_medio) / nullif(sum(r.poblacion), 0) AS homicidios
    FROM geo.zonas z
    JOIN census_tracts r ON ST_Contains(z.geom, r.punto)
    JOIN district_averages a USING (prov, ambito)
    GROUP BY z.osm_id
)
UPDATE geo.zona_indicadores i
SET delitos_anio = CASE WHEN i.delitos_fuente = 'snic' THEN round(i.delitos_anio * z.factor) ELSE i.delitos_anio END,
    delitos_robos = CASE WHEN i.delitos_fuente = 'snic' THEN round(i.delitos_robos * z.factor) ELSE i.delitos_robos END,
    delitos_hurtos = CASE WHEN i.delitos_fuente = 'snic' THEN round(i.delitos_hurtos * z.factor) ELSE i.delitos_hurtos END,
    delitos_lesiones = CASE WHEN i.delitos_fuente = 'snic' THEN round(i.delitos_lesiones * z.factor) ELSE i.delitos_lesiones END,
    delitos_homicidios = CASE WHEN i.delitos_fuente = 'snic' THEN round(i.delitos_homicidios * z.homicidios) ELSE i.delitos_homicidios END,
    delitos_amenazas = CASE WHEN i.delitos_fuente = 'snic' THEN round(i.delitos_amenazas * z.factor) ELSE i.delitos_amenazas END,
    delitos_comparables = round(i.delitos_comparables * sqrt(CASE WHEN i.delitos_fuente = 'snic' THEN z.factor ELSE 1 END * z.homicidios)),
    delitos_reparto = CASE WHEN i.delitos_fuente = 'snic' THEN z.factor END,
    delitos_reparto_homicidios = z.homicidios
FROM zone_factors z
-- Run only once after each calcular_zonas.sql execution; running it again would apply the factor twice.
WHERE i.osm_id = z.osm_id AND i.delitos_fuente IS NOT NULL AND i.delitos_reparto_homicidios IS NULL;

DROP TABLE geo.carga_delitos_fracciones;

COMMIT;

SELECT count(*) AS zonas, round(min(delitos_reparto)::numeric, 2) AS actividad_min,
       round(max(delitos_reparto)::numeric, 2) AS actividad_max,
       round(min(delitos_reparto_homicidios)::numeric, 2) AS homicidios_min,
       round(max(delitos_reparto_homicidios)::numeric, 2) AS homicidios_max
FROM geo.zona_indicadores
WHERE delitos_reparto_homicidios IS NOT NULL;
