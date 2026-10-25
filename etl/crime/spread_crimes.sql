-- Spreads the SNIC crimes of each partido over its localidades with the factor of each census fraction
-- (etl/crime/fit_crime_model.py, loaded in geo.carga_delitos_fracciones). Run by DATOS-ZONAS-DOCKER.bat right after
-- calcular_zonas.sql, which leaves every localidad with the rate of its partido.
--
-- Each radio of the province takes the partido rate times the factor of its fraction, divided by the partido's
-- population-weighted mean factor: the partido total does not change, only how it is shared. A zone's factor is the
-- mean of its radios. Registered crimes are multiplied by it; delitos_comparables, the geometric mean of estimated
-- crimes and partido homicides (which the model does not spread), by its square root.
-- CABA keeps the Mapa del Delito, which already locates each event.

\set ON_ERROR_STOP on
BEGIN;

WITH radios AS (
    SELECT r.cod, r.prov, r.depto, r.poblacion, r.punto, coalesce(f.factor, 1) AS factor
    FROM geo.censo_radios r
    LEFT JOIN geo.carga_delitos_fracciones f ON f.fraction_id = r.prov || r.depto || r.fracc
    WHERE r.prov = '06'
),
partidos AS (
    SELECT prov, depto, sum(poblacion * factor) / nullif(sum(poblacion), 0) AS factor_medio
    FROM radios
    GROUP BY prov, depto
),
zonas AS (
    SELECT z.osm_id, sum(r.poblacion * r.factor / p.factor_medio) / nullif(sum(r.poblacion), 0) AS factor
    FROM geo.zonas z
    JOIN radios r ON ST_Contains(z.geom, r.punto)
    JOIN partidos p USING (prov, depto)
    GROUP BY z.osm_id
)
UPDATE geo.zona_indicadores i
SET delitos_anio = round(i.delitos_anio * z.factor),
    delitos_robos = round(i.delitos_robos * z.factor),
    delitos_hurtos = round(i.delitos_hurtos * z.factor),
    delitos_lesiones = round(i.delitos_lesiones * z.factor),
    delitos_homicidios = round(i.delitos_homicidios * z.factor),
    delitos_amenazas = round(i.delitos_amenazas * z.factor),
    delitos_comparables = round(i.delitos_comparables * sqrt(z.factor)),
    delitos_reparto = z.factor
FROM zonas z
-- Only once per calcular_zonas.sql run: a second run would multiply again
WHERE i.osm_id = z.osm_id AND i.delitos_fuente = 'snic' AND i.delitos_reparto IS NULL;

DROP TABLE geo.carga_delitos_fracciones;

COMMIT;

SELECT count(*) AS zonas_repartidas, round(min(delitos_reparto)::numeric, 2) AS factor_minimo,
       round(max(delitos_reparto)::numeric, 2) AS factor_maximo
FROM geo.zona_indicadores
WHERE delitos_reparto IS NOT NULL;
