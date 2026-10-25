-- Spreads crimes inside each partido (and homicides inside each CABA comuna) with the factors of each census
-- fraction (etl/crime/fit_crime_model.py, loaded in geo.carga_delitos_fracciones). Run by DATOS-ZONAS-DOCKER.bat
-- right after calcular_zonas.sql, which gives every localidad the rates of its partido and every CABA barrio the
-- homicides of its comuna.
--
-- Two factors per zone, each the mean of its radios divided by the population-weighted mean of its partido or comuna,
-- so totals do not change, only how they are shared:
--   delitos_reparto            activity model (where crimes are reported): province only, CABA already has the
--                              Mapa del Delito by location. Registered crimes are multiplied by it.
--   delitos_reparto_homicidios NBI (where violence falls): province and CABA. Homicides of the province zones are
--                              multiplied by it.
-- delitos_comparables is the geometric mean of estimated crimes and homicides, so it takes the square root of each.

\set ON_ERROR_STOP on
BEGIN;

WITH radios AS (
    SELECT r.prov, r.poblacion, r.punto,
           -- Partido, or comuna in CABA (the census numbers comunas by 7)
           CASE WHEN r.prov = '02' THEN (r.depto::int / 7)::text ELSE r.depto END AS ambito,
           coalesce(f.factor, 1) AS factor, coalesce(f.homicide_factor, 1) AS homicidios
    FROM geo.censo_radios r
    LEFT JOIN geo.carga_delitos_fracciones f ON f.fraction_id = r.prov || r.depto || r.fracc
),
ambitos AS (
    SELECT prov, ambito,
           sum(poblacion * factor) / nullif(sum(poblacion), 0) AS factor_medio,
           sum(poblacion * homicidios) / nullif(sum(poblacion), 0) AS homicidios_medio
    FROM radios
    GROUP BY prov, ambito
),
zonas AS (
    SELECT z.osm_id,
           sum(r.poblacion * r.factor / a.factor_medio) / nullif(sum(r.poblacion), 0) AS factor,
           sum(r.poblacion * r.homicidios / a.homicidios_medio) / nullif(sum(r.poblacion), 0) AS homicidios
    FROM geo.zonas z
    JOIN radios r ON ST_Contains(z.geom, r.punto)
    JOIN ambitos a USING (prov, ambito)
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
FROM zonas z
-- Only once per calcular_zonas.sql run: a second run would multiply again
WHERE i.osm_id = z.osm_id AND i.delitos_fuente IS NOT NULL AND i.delitos_reparto_homicidios IS NULL;

DROP TABLE geo.carga_delitos_fracciones;

COMMIT;

SELECT count(*) AS zonas, round(min(delitos_reparto)::numeric, 2) AS actividad_min,
       round(max(delitos_reparto)::numeric, 2) AS actividad_max,
       round(min(delitos_reparto_homicidios)::numeric, 2) AS homicidios_min,
       round(max(delitos_reparto_homicidios)::numeric, 2) AS homicidios_max
FROM geo.zona_indicadores
WHERE delitos_reparto_homicidios IS NOT NULL;
